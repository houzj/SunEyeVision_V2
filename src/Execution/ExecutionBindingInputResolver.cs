using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>Resolves one invocation against admitted identities and retains source readers until it drains.</summary>
internal static class ExecutionBindingInputResolver
{
    internal sealed record Resolved(IReadOnlyDictionary<string, object?> Inputs,
        IReadOnlyDictionary<string, object?> Settings, IReadOnlyList<IDisposable> Readers);

    internal static Resolved Resolve(ExecutionWorkflowSnapshot workflow, ExecutionNodeSnapshot node,
        ExecutionBindingPlan.NodePlan plan, ToolExecutionIdentity identity,
        IReadOnlyDictionary<BindingNodeId, RunIdentity> expected,
        IReadOnlyDictionary<string, DeclaredOutputSnapshot> results, ExecutionVariableValues? variables,
        RuntimeParameterValueAcceptance? parameterValues)
    {
        if (plan.Issues.Count != 0) throw new InvalidOperationException(string.Join("；", plan.Issues.Select(issue => issue.Message)));
        var readers = new List<IDisposable>();
        try
        {
            var actual = new List<BindingNodeResult>();
            foreach (var dependency in plan.References.Values.SelectMany(binding => binding.Dependencies).Distinct())
            {
                if (dependency.ScopeId != workflow.WorkflowId || !results.TryGetValue(dependency.NodeId, out var result)) continue;
                var reader = result.AcquireReader();
                readers.Add(reader);
                if (reader.Values is not { } values) continue;
                actual.Add(new BindingNodeResult(dependency, result.Identity, result.SchemaRevision,
                    result.Schema.Fingerprint, result.IsSuccess, values));
            }
            var invocation = identity.ToInvocationIdentity(node.NodeId);
            var snapshot = new BindingRunSnapshot(new(workflow.WorkflowId, node.NodeId), invocation,
                workflow.DocumentRevision, actual, expected, variables?.Capture(invocation));
            var settingsIds = node.SettingsSchema.Inputs.Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
            var targets = node.BindingTargets.ToDictionary(port => port.Id, StringComparer.Ordinal);
            var inputs = new Dictionary<string, object?>(StringComparer.Ordinal);
            var settings = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var (fieldId, setting) in node.CaptureExecutionFields())
            {
                if (!targets.TryGetValue(fieldId, out var port)) throw new InvalidOperationException("未知运行输入或设置：" + fieldId);
                var isSetting = settingsIds.Contains(fieldId);
                var destination = isSetting ? settings : inputs;
                if (setting is ConstantSetting constant)
                {
                    if (port.EditModes != FieldEditModes.None && (port.EditModes & FieldEditModes.Constant) == 0)
                        throw new InvalidOperationException("运行输入不能使用持久常量。");
                    if (!isSetting) destination.Add(fieldId, ValueRules.Freeze(port, constant.Value));
                    continue;
                }
                BindingResolution resolved;
                if (setting is VariableSetting)
                {
                    if (variables is null || !plan.Variables.TryGetValue(fieldId, out var variable))
                        throw new InvalidOperationException("运行变量没有本批已编译计划。" );
                    resolved = VariableBindingCompiler.Resolve(variable, variables.SolutionId, snapshot);
                }
                else
                {
                    if (!plan.References.TryGetValue(fieldId, out var reference))
                        throw new InvalidOperationException("运行输入没有已准入绑定计划：" + fieldId);
                    resolved = new ParameterBindingService().ResolveBinding(reference, snapshot);
                }
                if (!resolved.Success) throw new InvalidOperationException(resolved.Diagnostic?.Message ?? "运行绑定解析失败。");
                destination.Add(fieldId, resolved.Value);
                if (isSetting) (parameterValues ?? throw new InvalidOperationException("引用参数缺少正式宿主的当前值接纳入口。"))
                    .Accept(node, identity, fieldId, resolved.Value);
            }
            return new Resolved(inputs, settings, readers.AsReadOnly());
        }
        catch (Exception failure)
        {
            var errors = new List<Exception> { failure };
            foreach (var reader in readers) try { reader.Dispose(); } catch (Exception cleanup) { errors.Add(cleanup); }
            if (errors.Count > 1) throw new AggregateException("绑定解析和 reader 释放均失败。", errors);
            throw;
        }
    }
}
