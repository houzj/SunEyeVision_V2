using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>Document-issued capability for accepting a parameter value from one actual invocation.</summary>
public sealed class RuntimeParameterValueAcceptance
{
    private readonly Action<ExecutionNodeSnapshot, ToolExecutionIdentity, string, object?> _accept;
    internal RuntimeParameterValueAcceptance(Action<ExecutionNodeSnapshot, ToolExecutionIdentity, string, object?> accept)
        => _accept = accept ?? throw new ArgumentNullException(nameof(accept));

    internal void Accept(ExecutionNodeSnapshot node, ToolExecutionIdentity identity, string fieldId, object? value)
    {
        ArgumentNullException.ThrowIfNull(node); ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (!ReferenceEquals(identity.Owner, node.Owner) || identity.NodeId != node.OwnerDomainId)
            throw new InvalidOperationException("运行值身份不属于本次捕获的节点。");
        _accept(node, identity, fieldId, value);
    }
}
