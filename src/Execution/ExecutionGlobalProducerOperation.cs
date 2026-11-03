using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Execution;

/// <summary>Frozen scalar or sequence assignment from one admitted output to one global variable.</summary>
public sealed class ExecutionGlobalProducerOperation
{
    internal ExecutionGlobalProducerOperation(string variableId, string variableName, OutputReference source, Port target, Type valueType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variableId); ArgumentException.ThrowIfNullOrWhiteSpace(variableName);
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(target); ArgumentNullException.ThrowIfNull(valueType);
        if (string.IsNullOrWhiteSpace(source.ScopeId) || source.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("全局生产引用必须含作用域并选择全部数据。", nameof(source));
        var element = target.Type.Shape == ValueShape.Sequence ? target.Type.Element! : target.Type;
        if (element.Shape != ValueShape.Scalar) throw new ArgumentException("全局变量生产仅支持纯标量及序列。", nameof(target));
        VariableId = variableId; VariableName = variableName; Source = source; Target = target; ValueType = valueType;
    }
    public string VariableId { get; }
    public string VariableName { get; }
    public OutputReference Source { get; }
    public Port Target { get; }
    public Type ValueType { get; }
    internal string OperationId => "global-producer:" + VariableId.Length + ":" + VariableId;
}
