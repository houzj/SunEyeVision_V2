using System.Text.Json;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

internal sealed record ProducerBindingValidation(BindingCompatibility State, string Reason, Type? ValueType = null);

/// <summary>Checks a producer against the session's admitted declarations, without reading a run result.</summary>
internal static class GlobalProducerBindingRules
{
    internal static bool TryDescribeTarget(string vocabulary, out Port target, out Type clrType, out string reason)
    {
        target = null!; clrType = null!; reason = string.Empty;
        if (!VariableValueDeclarations.TryGetPort(vocabulary, out target))
        {
            reason = "全局变量尚无资源、组合或几何的正式存储及租约合同，不能建立此生产绑定。";
            return false;
        }
        var scalarType = target.Type.Shape == ValueShape.Sequence ? target.Type.Element! : target.Type;
        var scalar = scalarType.Scalar switch
        {
            ScalarKind.Boolean => typeof(bool), ScalarKind.Text => typeof(string),
            ScalarKind.Int64 => target.Storage == NumericStorage.Int32 ? typeof(int) : typeof(long),
            ScalarKind.Float64 => target.Storage == NumericStorage.Float32 ? typeof(float) : typeof(double),
            _ => throw new InvalidOperationException("变量声明不是纯值。")
        };
        clrType = target.Type.Shape == ValueShape.Sequence ? scalar.MakeArrayType() : scalar;
        return true;
    }

    // The caller owns a frozen candidate and supplies its actual members and a declaration reader
    // that has already checked the session owner, directory and generation. No node self-admission.
    internal static ProducerBindingValidation Validate(ProducerBinding producer, GlobalVariable variable,
        Func<string, IReadOnlyList<NodeDefinition>?> findScope, Func<NodeDefinition, ToolSchema?> readContract,
        Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(producer); ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(findScope); ArgumentNullException.ThrowIfNull(readContract);
        ArgumentNullException.ThrowIfNull(isCurrent);
        static ProducerBindingValidation Reject(string reason) => new(BindingCompatibility.Incompatible, reason);
        try
        {
            var expression = producer.ReferenceExpression;
            var vocabulary = variable.Type; var variableId = variable.Id;
            var reference = producer.SourceReference;
            if (string.IsNullOrWhiteSpace(reference.ScopeId)) return Reject("全局变量生产来源必须声明所属流程作用域。");
            if (reference.Selection is not BindingSelection.AllItems)
                return Reject("全局变量生产绑定仅支持声明字段及集合 All。");
            if (!TryDescribeTarget(vocabulary, out var target, out var clrType, out var reason)) return Reject(reason);
            var members = findScope(reference.ScopeId);
            if (members is null) return Reject("全局变量生产来源流程已删除或作用域身份不唯一。");
            var candidates = members.Where(node => node.Id == reference.NodeId).ToArray();
            if (candidates.Length != 1) return Reject("全局变量生产节点已删除或身份不唯一，请重新绑定。");
            var source = candidates[0]; var configuration = source.Configuration; var toolType = source.ToolType;
            bool Current() => isCurrent() && producer.ReferenceExpression == expression && variable.Type == vocabulary &&
                variable.Id == variableId && source.Id == reference.NodeId && source.ToolType == toolType &&
                ReferenceEquals(source.Configuration, configuration) &&
                findScope(reference.ScopeId) is { } current && current.Count(node => node.Id == reference.NodeId) == 1 &&
                current.Count(node => ReferenceEquals(node, source)) == 1;
            var contract = readContract(source);
            if (!Current()) return Reject("读取生产来源声明期间配置或节点归属已改变，请重新选择当前来源。");
            if (contract is null) return new(BindingCompatibility.Unresolved, "生产来源正式输出声明暂不可用，等待校验。");
            var selected = contract.Outputs.Where(output => output.Id == reference.PortId).ToArray();
            if (selected.Length != 1) return Reject("全局变量原输出身份已删除或不唯一，请重新绑定。");
            var schema = new ToolSchema(Array.Empty<Port>(), new[] { selected[0] });
            var nodeId = new BindingNodeId(reference.ScopeId, reference.NodeId);
            var scope = new BindingScopeSnapshot(new(reference.ScopeId, "global-producer:" + variable.Id), Guid.Empty, 0,
                new[] { new BindingSource(nodeId, Guid.Empty, schema, 0) }, new[] { nodeId },
                Array.Empty<KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>>());
            var compiled = new ParameterBindingService().CompileBinding(reference, target, scope);
            if (!Current()) return Reject("编译生产来源期间配置或节点归属已改变，请重新选择当前来源。");
            return compiled.Plan is null ? Reject(compiled.Diagnostic?.Message ?? "全局变量生产来源声明不符合目标存储类型。")
                : new(compiled.Compatibility, "正式生产来源声明可绑定；运行时仍须当前批次准入与值校验。", clrType);
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        { return Reject("全局变量生产绑定无效：" + failure.Message); }
    }
}
