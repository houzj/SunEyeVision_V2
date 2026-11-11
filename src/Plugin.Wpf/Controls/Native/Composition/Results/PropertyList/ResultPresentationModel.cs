using System.Globalization;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.PropertyList;

public enum ResultPresentationValueState { Unavailable, Missing, Null, Available, ResourcePending }

/// <summary>A declaration or one explicitly identified run; it never reads a latest-result cache.</summary>
public sealed class ResultPresentationModel
{
    private ResultPresentationModel(ToolSchema schema, ResultPublicationStamp stamp, ResultViewState state, int pageSize, InputValues? values, Func<bool>? isCurrent,ResultValueAccess access)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (state is ResultViewState.Success or ResultViewState.PartialSuccess) ArgumentNullException.ThrowIfNull(values);
        var hasValues=values is not null&&(state is ResultViewState.Success or ResultViewState.PartialSuccess or ResultViewState.Failed or ResultViewState.Timeout);
        Schema = schema;
        Stamp=stamp;
        Identity = stamp.Invocation;
        State = state;
        PageSize = pageSize;
        // VM's result declaration owns the visible root order. Keep the declared
        // port identities and value contract intact, while projecting roots in
        // OutputDisplay order. Tools without a display declaration retain the
        // schema order as a deterministic fallback.
        var outputOrder = schema.OutputDisplay.Select(node => node.FieldId)
            .Concat(schema.Outputs.Select(port => port.Id))
            .Distinct(StringComparer.Ordinal)
            .Select(id => schema.Outputs.Single(port => port.Id == id))
            .ToArray();
        var outputs = new ResultPresentationNode[outputOrder.Length];
        for (var index = 0; index < outputs.Length; index++)
        {
            var port = outputOrder[index];
            // A resource leaf is never read. Mixed containers are observed only transiently;
            // their navigation path keeps weak publication/lease identities, not a parent reader.
            var reader=hasValues&&port.Type.Shape!=ValueShape.Resource?values!.Read(port.Id):null;
            outputs[index]=new ResultPresentationNode(PortKey(port.Id),port.Type,port,null,pageSize,reader,
                isCurrent:isCurrent,path:access.Root(port.Id),address:new ResultDeclarationAddress(port));
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
        var model=new ResultPresentationModel(publication.Schema,stamp,publication.State,pageSize,lease.Values,isCurrent,new ResultValueAccess(publication,lease,stamp));
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

/// <summary>Immutable declaration identity, independent of a value lease or a UI display key.</summary>
public sealed class ResultDeclarationAddress
{
    public ResultDeclarationAddress(Port rootPort):this(rootPort,Array.Empty<string>(),false,false){}
    private ResultDeclarationAddress(Port rootPort,IEnumerable<string> members,bool sample,bool sampleRow)
    {RootPort=rootPort;MemberFieldPath=Array.AsReadOnly(members.ToArray());ThroughSequence=sample;IsSampleRow=sampleRow;}
    public Port RootPort {get;}
    public IReadOnlyList<string> MemberFieldPath {get;}
    public bool ThroughSequence {get;}
    public bool IsSampleRow {get;}
    internal ResultDeclarationAddress Field(string id)=>new(RootPort,MemberFieldPath.Append(id),ThroughSequence,IsSampleRow);
    internal ResultDeclarationAddress Sample()=>new(RootPort,MemberFieldPath,true,true);
    public OutputReference Reference(string nodeId,string scopeId)=>new(nodeId,RootPort.Id,RootPort.Type,MemberFieldPath,BindingSelection.All,scopeId,RootPort.Storage);
}

/// <summary>Children are materialized only for requested pages; cached nodes retain expansion state.</summary>
public sealed class ResultPresentationNode
{
    private readonly Func<bool>? _isCurrent;
    private readonly ResultValuePath? _path;
    private bool Current=>_isCurrent?.Invoke()!=false&&(_path?.IsCurrent??true)&&_isCurrent?.Invoke()!=false;
    private DeclaredValueReader? Reader=>_reader??_path?.Read();
    private readonly int _pageSize;
    private readonly DeclaredValueReader? _reader;
    private readonly GeometryValue? _geometry;
    private readonly Dictionary<int, ResultPresentationNode> _children = new();

    internal ResultPresentationNode(string key, TypeRef declaration, Port? port, int? elementIndex, int pageSize, DeclaredValueReader? reader = null,GeometryValue? geometry=null,Func<bool>? isCurrent=null,ResultValuePath? path=null,ResultDeclarationAddress? address=null,bool allProjection=false,NumericStorage? elementStorage=null)
    {
        _isCurrent=isCurrent;_path=path;DeclarationAddress=address;IsAllProjection=allProjection;
        Key = key;
        Declaration = declaration;
        DeclaredPort = port;
        PhysicalStorage = port?.Storage ?? elementStorage;
        ElementIndex = elementIndex;
        _pageSize = pageSize;
        if (reader != null && reader.Declaration != declaration)
            throw new ArgumentException("The supplied run value does not match the declared field type.", nameof(reader));
        ValueState = declaration.Shape==ValueShape.Resource ? ResultPresentationValueState.ResourcePending
            : reader == null ? geometry is null?ResultPresentationValueState.Unavailable:ResultPresentationValueState.Available
            : reader.IsMissing ? ResultPresentationValueState.Missing
            : reader.IsNull ? ResultPresentationValueState.Null
            : ResultPresentationValueState.Available;
        _reader=ValueState==ResultPresentationValueState.Available&&!ResultPresentationModel.ContainsResource(declaration)?reader:null;
        _geometry=geometry??(_reader is not null&&declaration.Shape==ValueShape.Geometry?_reader.GetGeometry():null);
    }

    public bool IsAllProjection {get;}
    public bool HasAllMembers=>Declaration.Shape==ValueShape.Sequence&&Declaration.Element!.Shape==ValueShape.Record;
    public ResultPresentationNode? CreateAllMembers()
        =>!Current||!HasAllMembers||IsAllProjection?null:new(Key+"/all",Declaration,null,null,_pageSize,isCurrent:_isCurrent,path:_path,address:DeclarationAddress,allProjection:true);
    public ResultDeclarationAddress? DeclarationAddress {get;}
    public string Key { get; }
    public TypeRef Declaration { get; }
    public Port? DeclaredPort { get; }
    public NumericStorage? PhysicalStorage { get; }
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
    public int? ChildCount => !Current?null:IsAllProjection?(HasAllMembers?Declaration.Element!.Fields.Count:0):Declaration.Shape switch
    {
        ValueShape.Record => Declaration.Fields.Count,
        ValueShape.Sequence => SequenceCount(),
        ValueShape.Geometry => _geometry is PolygonGeometry polygon?polygon.Vertices.Count:0,
        _ => 0
    };

    private int? SequenceCount()
    {
        var reader=Reader;
        var count=reader is null||reader.IsMissing||reader.IsNull?(int?)null:reader.Count;
        return Current?count:null;
    }
    private DeclaredValueReader? ChildReader(string? field,int? index,TypeRef type)
    {
        if(type.Shape==ValueShape.Resource)return null;
        var reader=Reader;
        if(reader is null||reader.IsMissing||reader.IsNull)return null;
        return field is not null?reader.GetField(field):reader.GetElement(index!.Value);
    }

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
                if(IsAllProjection&&HasAllMembers)
                {
                    var field=Declaration.Element!.Fields[index];
                    child=new ResultPresentationNode(Key+"/f"+field.Id.Length+":"+field.Id,TypeRef.SequenceOf(field.Type,Declaration.Count),field,null,_pageSize,
                        isCurrent:_isCurrent,path:_path,address:DeclarationAddress?.Field(field.Id),allProjection:true);
                }
                else if (_geometry is PolygonGeometry polygon)
                {
                    var key=Key+"/vertex"+index.ToString(CultureInfo.InvariantCulture);
                    child=new ResultPresentationNode(key,TypeRef.GeometryOf(GeometryKind.Point,polygon.Frame),null,index,_pageSize,
                        geometry:new PointGeometry(polygon.Frame,polygon.Vertices[index]),isCurrent:_isCurrent,path:_path);
                }
                else if (Declaration.Shape == ValueShape.Record)
                {
                    var field = Declaration.Fields[index];
                    var key = Key + "/f" + field.Id.Length.ToString(CultureInfo.InvariantCulture) + ":" + field.Id;
                    child = new ResultPresentationNode(key, field.Type, field, null, _pageSize, ChildReader(field.Id,null,field.Type),isCurrent:_isCurrent,path:_path?.Field(field.Id),address:DeclarationAddress?.Field(field.Id));
                }
                else
                {
                    var key = Key + "/i" + index.ToString(CultureInfo.InvariantCulture);
                    child = new ResultPresentationNode(key, Declaration.Element!, null, index, _pageSize, ChildReader(null,index,Declaration.Element!),isCurrent:_isCurrent,path:_path?.Element(index),address:DeclarationAddress?.Sample(),elementStorage:PhysicalStorage);
                }
                if(!Current)return Array.Empty<ResultPresentationNode>();
                _children.Add(index, child);
            }
            page[position] = child;
        }
        return Array.AsReadOnly(page);
    }
}

/// <summary>Only weak access to the consumer-owned lease. Never captures values or resource readers.</summary>
internal sealed class ResultValueAccess
{
    private readonly WeakReference<IResultPublication> _publication;
    private readonly WeakReference<IResultValueLease> _lease;
    private readonly ResultPublicationStamp _stamp;
    internal ResultValueAccess(IResultPublication publication,IResultValueLease lease,ResultPublicationStamp stamp)
    {_publication=new(publication);_lease=new(lease);_stamp=stamp;}
    internal bool IsCurrent=>TryValues(out _);
    internal ResultValuePath Root(string id)=>new(this,id);
    internal bool TryValues(out InputValues? values)
    {
        values=null;
        if(!_publication.TryGetTarget(out var publication)||!_lease.TryGetTarget(out var lease))return false;
        try
        {
            if(publication.Stamp!=_stamp||lease.Stamp!=_stamp||!publication.IsCurrent())return false;
            var current=lease.Values;
            if(publication.Stamp!=_stamp||lease.Stamp!=_stamp||!publication.IsCurrent())return false;
            values=current;return true;
        }
        catch(ObjectDisposedException){return false;}
    }
}

/// <summary>Stable output/field/index path; no parent value or reader is retained.</summary>
internal sealed class ResultValuePath(ResultValueAccess access,string output,ResultValuePath? parent=null,string? field=null,int? index=null)
{
    private readonly ResultValuePath? _parent=parent;
    private readonly string? _field=field;
    private readonly int? _index=index;
    internal bool IsCurrent=>access.IsCurrent;
    internal ResultValuePath Field(string id)=>new(access,output,this,field:id);
    internal ResultValuePath Element(int position)=>new(access,output,this,index:position);
    internal DeclaredValueReader? Read()
    {
        if(!access.TryValues(out var values)||values is null)return null;
        var reader=values.Read(output);
        var steps=new Stack<ResultValuePath>();
        for(var path=this;path._parent is not null;path=path._parent)steps.Push(path);
        while(steps.TryPop(out var step))
        {
            if(reader.IsMissing||reader.IsNull)return null;
            reader=step._field is not null?reader.GetField(step._field):reader.GetElement(step._index!.Value);
        }
        return access.IsCurrent?reader:null;
    }
}
