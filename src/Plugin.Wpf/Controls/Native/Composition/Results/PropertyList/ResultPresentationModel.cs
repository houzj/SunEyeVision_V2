using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

public enum ResultPresentationValueState { Unavailable, Missing, Null, Available, ResourcePending }

/// <summary>A declaration or one explicitly identified run; it never reads a latest-result cache.</summary>
public sealed class ResultPresentationModel
{
    private ResultPresentationModel(ToolSchema schema, ResultPublicationStamp stamp, ResultViewState state, int pageSize, InputValues? values, Func<bool>? isCurrent)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        var hasValues=state is ResultViewState.Success or ResultViewState.PartialSuccess;
        if (hasValues) ArgumentNullException.ThrowIfNull(values);
        Schema = schema;
        Stamp=stamp;
        Identity = stamp.Invocation;
        State = state;
        PageSize = pageSize;
        var outputs = new ResultPresentationNode[schema.Outputs.Count];
        for (var index = 0; index < outputs.Length; index++)
        {
            var port = schema.Outputs[index];
            // Do not retain InputValues or readers for aggregates that may contain borrowed resources.
            var reader = hasValues && !ContainsResource(port.Type)
                ? values!.Read(port.Id) : null;
            outputs[index] = new ResultPresentationNode(PortKey(port.Id), port.Type, port, null, pageSize, reader,isCurrent:isCurrent);
        }
        Outputs = Array.AsReadOnly(outputs);
    }

    public ResultPublicationStamp Stamp { get; }
    public ToolSchema Schema { get; }
    public RunIdentity? Identity { get; }
    public ResultViewState State { get; }
    public int PageSize { get; }
    public IReadOnlyList<ResultPresentationNode> Outputs { get; }

    public static ResultPresentationModel ForPublication(IResultPublication publication,IResultValueLease lease,int pageSize=128,Func<bool>? isCurrent=null)
    {
        ArgumentNullException.ThrowIfNull(publication);ArgumentNullException.ThrowIfNull(lease);
        var stamp=publication.Stamp;
        if(!publication.IsCurrent()||stamp!=lease.Stamp||stamp.SchemaFingerprint!=publication.Schema.Fingerprint)
            throw new InvalidOperationException("Result publication lease is stale or has a different identity.");
        var model=new ResultPresentationModel(publication.Schema,stamp,publication.State,pageSize,lease.Values,isCurrent);
        if(!publication.IsCurrent()||publication.Stamp!=stamp||lease.Stamp!=stamp||isCurrent?.Invoke()==false)
            throw new InvalidOperationException("Result publication changed while reading declared values.");
        return model;
    }

    internal static string PortKey(string id) => "p" + id.Length.ToString(CultureInfo.InvariantCulture) + ":" + id;
    internal static bool ContainsResource(TypeRef type) => type.Shape switch
    {
        ValueShape.Resource => true,
        ValueShape.Record => type.Fields.Any(field => ContainsResource(field.Type)),
        ValueShape.Sequence => ContainsResource(type.Element!),
        _ => false
    };
}

/// <summary>Children are materialized only for requested pages; cached nodes retain expansion state.</summary>
public sealed class ResultPresentationNode
{
    private readonly Func<bool>? _isCurrent;
    private bool Current=>_isCurrent?.Invoke()!=false;
    private readonly int _pageSize;
    private readonly DeclaredValueReader? _reader;
    private readonly GeometryValue? _geometry;
    private readonly Dictionary<int, ResultPresentationNode> _children = new();

    internal ResultPresentationNode(string key, TypeRef declaration, Port? port, int? elementIndex, int pageSize, DeclaredValueReader? reader = null,GeometryValue? geometry=null,Func<bool>? isCurrent=null)
    {
        _isCurrent=isCurrent;
        Key = key;
        Declaration = declaration;
        DeclaredPort = port;
        ElementIndex = elementIndex;
        _pageSize = pageSize;
        if (reader != null && reader.Declaration != declaration)
            throw new ArgumentException("The supplied run value does not match the declared field type.", nameof(reader));
        ValueState = ResultPresentationModel.ContainsResource(declaration) ? ResultPresentationValueState.ResourcePending
            : reader == null ? geometry is null?ResultPresentationValueState.Unavailable:ResultPresentationValueState.Available
            : reader.IsMissing ? ResultPresentationValueState.Missing
            : reader.IsNull ? ResultPresentationValueState.Null
            : ResultPresentationValueState.Available;
        _reader = ValueState == ResultPresentationValueState.Available ? reader : null;
        _geometry=geometry??(_reader is not null&&declaration.Shape==ValueShape.Geometry?_reader.GetGeometry():null);
    }

    public string Key { get; }
    public TypeRef Declaration { get; }
    public Port? DeclaredPort { get; }
    public int? ElementIndex { get; }
    public bool IsExpanded { get; set; }
    public ResultPresentationValueState ValueState { get; }
    /// <summary>Only immutable canonical scalars escape; containers and resource payloads never do.</summary>
    public object? ScalarValue => Current && _reader != null && Declaration.Shape == ValueShape.Scalar
        ? Declaration.Scalar switch
        {
            ScalarKind.Boolean => _reader.GetScalar<bool>(),
            ScalarKind.Int64 => _reader.GetScalar<long>(),
            ScalarKind.Float64 => _reader.GetScalar<double>(),
            ScalarKind.Text => _reader.GetScalar<string>(),
            _ => null
        } : null;
    public GeometryValue? GeometryValue => Current?_geometry:null;
    // A declared count range is not an observed element count, including Exactly(0/1/N).
    public int? ChildCount => !Current?null:Declaration.Shape switch
    {
        ValueShape.Record => Declaration.Fields.Count,
        ValueShape.Sequence => _reader?.Count,
        ValueShape.Geometry => _geometry is PolygonGeometry polygon?polygon.Vertices.Count:0,
        _ => 0
    };

    public IReadOnlyList<ResultPresentationNode> GetChildrenPage(int offset, int count)
    {
        if(!Current)return Array.Empty<ResultPresentationNode>();
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || count > _pageSize) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0 || ChildCount is not int total || offset >= total)
            return Array.Empty<ResultPresentationNode>();
        var length = Math.Min(count, total - offset);
        var page = new ResultPresentationNode[length];
        for (var position = 0; position < length; position++)
        {
            if(!Current)return Array.Empty<ResultPresentationNode>();
            var index = offset + position;
            if (!_children.TryGetValue(index, out var child))
            {
                if (_geometry is PolygonGeometry polygon)
                {
                    var key=Key+"/vertex"+index.ToString(CultureInfo.InvariantCulture);
                    child=new ResultPresentationNode(key,TypeRef.GeometryOf(GeometryKind.Point,polygon.Frame),null,index,_pageSize,
                        geometry:new PointGeometry(polygon.Frame,polygon.Vertices[index]),isCurrent:_isCurrent);
                }
                else if (Declaration.Shape == ValueShape.Record)
                {
                    var field = Declaration.Fields[index];
                    var key = Key + "/f" + field.Id.Length.ToString(CultureInfo.InvariantCulture) + ":" + field.Id;
                    child = new ResultPresentationNode(key, field.Type, field, null, _pageSize, _reader?.GetField(field.Id),isCurrent:_isCurrent);
                }
                else
                {
                    var key = Key + "/i" + index.ToString(CultureInfo.InvariantCulture);
                    child = new ResultPresentationNode(key, Declaration.Element!, null, index, _pageSize, _reader!.GetElement(index),isCurrent:_isCurrent);
                }
                _children.Add(index, child);
            }
            page[position] = child;
        }
        return Array.AsReadOnly(page);
    }
}
