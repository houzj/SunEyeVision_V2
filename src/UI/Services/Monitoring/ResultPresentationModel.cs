using System.Globalization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.UI.Services.Monitoring;

public enum ResultPresentationState { NotRun, Success, Failed }
public enum ResultPresentationValueState { Unavailable, Missing, Null, Available, ResourcePending }

/// <summary>A declaration or one explicitly identified run; it never reads a latest-result cache.</summary>
public sealed class ResultPresentationModel
{
    private ResultPresentationModel(ToolSchema schema, RunIdentity? identity, ResultPresentationState state, int pageSize, InputValues? values = null)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (pageSize <= 0) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (!Enum.IsDefined(state)) throw new ArgumentOutOfRangeException(nameof(state));
        if (state == ResultPresentationState.Success) ArgumentNullException.ThrowIfNull(values);
        Schema = schema;
        Identity = identity;
        State = state;
        PageSize = pageSize;
        var outputs = new ResultPresentationNode[schema.Outputs.Count];
        for (var index = 0; index < outputs.Length; index++)
        {
            var port = schema.Outputs[index];
            // Do not retain InputValues or readers for aggregates that may contain borrowed resources.
            var reader = state == ResultPresentationState.Success && !ContainsResource(port.Type)
                ? values!.Read(port.Id) : null;
            outputs[index] = new ResultPresentationNode(PortKey(port.Id), port.Type, port, null, pageSize, reader);
        }
        Outputs = Array.AsReadOnly(outputs);
    }

    public ToolSchema Schema { get; }
    public RunIdentity? Identity { get; }
    public ResultPresentationState State { get; }
    public int PageSize { get; }
    public IReadOnlyList<ResultPresentationNode> Outputs { get; }

    public static ResultPresentationModel ForSchema(ToolSchema schema, int pageSize = 128) =>
        new(schema, null, ResultPresentationState.NotRun, pageSize);

    public static ResultPresentationModel ForRun(ToolSchema schema, RunIdentity identity,
        ResultPresentationState state, InputValues? values, int pageSize = 128) =>
        new(schema, identity, state, pageSize, values);

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
    private readonly int _pageSize;
    private readonly DeclaredValueReader? _reader;
    private readonly Dictionary<int, ResultPresentationNode> _children = new();

    internal ResultPresentationNode(string key, TypeRef declaration, Port? port, int? elementIndex, int pageSize, DeclaredValueReader? reader = null)
    {
        Key = key;
        Declaration = declaration;
        DeclaredPort = port;
        ElementIndex = elementIndex;
        _pageSize = pageSize;
        if (reader != null && reader.Declaration != declaration)
            throw new ArgumentException("The supplied run value does not match the declared field type.", nameof(reader));
        ValueState = ResultPresentationModel.ContainsResource(declaration) ? ResultPresentationValueState.ResourcePending
            : reader == null ? ResultPresentationValueState.Unavailable
            : reader.IsMissing ? ResultPresentationValueState.Missing
            : reader.IsNull ? ResultPresentationValueState.Null
            : ResultPresentationValueState.Available;
        _reader = ValueState == ResultPresentationValueState.Available ? reader : null;
    }

    public string Key { get; }
    public TypeRef Declaration { get; }
    public Port? DeclaredPort { get; }
    public int? ElementIndex { get; }
    public bool IsExpanded { get; set; }
    public ResultPresentationValueState ValueState { get; }
    /// <summary>Only immutable canonical scalars escape; containers and resource payloads never do.</summary>
    public object? ScalarValue => _reader != null && Declaration.Shape == ValueShape.Scalar
        ? Declaration.Scalar switch
        {
            ScalarKind.Boolean => _reader.GetScalar<bool>(),
            ScalarKind.Int64 => _reader.GetScalar<long>(),
            ScalarKind.Float64 => _reader.GetScalar<double>(),
            ScalarKind.Text => _reader.GetScalar<string>(),
            _ => null
        } : null;
    // A declared count range is not an observed element count, including Exactly(0/1/N).
    public int? ChildCount => Declaration.Shape switch
    {
        ValueShape.Record => Declaration.Fields.Count,
        ValueShape.Sequence => _reader?.Count,
        _ => 0
    };

    public IReadOnlyList<ResultPresentationNode> GetChildrenPage(int offset, int count)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
        if (count < 0 || count > _pageSize) throw new ArgumentOutOfRangeException(nameof(count));
        if (count == 0 || ChildCount is not int total || offset >= total)
            return Array.Empty<ResultPresentationNode>();
        var length = Math.Min(count, total - offset);
        var page = new ResultPresentationNode[length];
        for (var position = 0; position < length; position++)
        {
            var index = offset + position;
            if (!_children.TryGetValue(index, out var child))
            {
                if (Declaration.Shape == ValueShape.Record)
                {
                    var field = Declaration.Fields[index];
                    var key = Key + "/f" + field.Id.Length.ToString(CultureInfo.InvariantCulture) + ":" + field.Id;
                    child = new ResultPresentationNode(key, field.Type, field, null, _pageSize, _reader?.GetField(field.Id));
                }
                else
                {
                    var key = Key + "/i" + index.ToString(CultureInfo.InvariantCulture);
                    child = new ResultPresentationNode(key, Declaration.Element!, null, index, _pageSize, _reader!.GetElement(index));
                }
                _children.Add(index, child);
            }
            page[position] = child;
        }
        return Array.AsReadOnly(page);
    }
}
