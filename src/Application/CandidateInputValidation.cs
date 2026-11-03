using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

/// <summary>Validates participant references against a frozen candidate, not stale published results.</summary>
internal static class CandidateInputValidation
{
    internal static void Validate(string solutionId, string workflowId, Guid ownerId, long revision,
        GraphState graph, IReadOnlyList<GlobalVariable> variables, IReadOnlySet<string> participants,
        bool validateAll, Func<NodeDefinition, ToolConfiguration, AdmittedToolConfiguration> admit, bool requireExecutionInputs = true)
    {
        var configs = graph.Nodes.ToDictionary(item => item.Node.Id, item => admit(item.Node, item.Configuration), StringComparer.Ordinal);
        var sources = configs.Select(pair => new BindingSource(new(workflowId, pair.Key), Guid.Empty, pair.Value.Schema, 0)).ToArray();
        var variablePorts = new Dictionary<string, Port>(StringComparer.Ordinal);
        var variableSources = new List<BindingVariableSource>();
        foreach (var variable in variables)
        {
            if (!GlobalProducerBindingRules.TryDescribeTarget(variable.Type, out var port, out _, out _)) continue;
            variablePorts.Add(variable.Id, port);
            var producer = variable.ProducerBinding?.SourceReference;
            variableSources.Add(new BindingVariableSource(new VariableReference(solutionId, variable.Id, port.Type, storage: port.Storage),
                producer is not null && !string.IsNullOrWhiteSpace(producer.ScopeId)
                    ? new BindingNodeId(producer.ScopeId, producer.NodeId) : null));
        }
        foreach (var item in graph.Nodes)
        {
            if (!validateAll && !participants.Contains(item.Node.Id)) continue;
            var configuration = configs[item.Node.Id];
            var diagnostic = (requireExecutionInputs ? configuration.ValidateForExecution() : configuration.ValidateForEditing()).FirstOrDefault();
            if (diagnostic is not null) throw new InvalidOperationException(diagnostic.Message);
            var scope = new BindingScopeSnapshot(new(workflowId, item.Node.Id), ownerId, revision,
                sources, sources.Select(source => source.Node),
                Array.Empty<KeyValuePair<BindingNodeId, IReadOnlyList<BindingNodeId>>>(), variableSources);
            var qualification = new ManualReferenceQualification(workflowId, graph, item.Node.Id, admit);
            foreach (var (fieldId, setting) in configuration.Configuration.Fields.Capture())
            {
                var target = configuration.Schema.Inputs.Concat(configuration.SettingsSchema.Inputs)
                    .Concat(configuration.DisplaySchema.Inputs).Single(port => port.Id == fieldId);
                var behavior = configuration.ReferenceBehaviors.GetValueOrDefault(fieldId, ParameterReferenceBehavior.StrictExactlyOne);
                var numericParameter = configuration.SettingsSchema.Inputs.Any(port => port.Id == fieldId &&
                    port.Type.Shape == ValueShape.Scalar && port.Type.Scalar is ScalarKind.Int64 or ScalarKind.Float64);
                var qualified = numericParameter ? scope.WithReferenceQualification(qualification.Validate) : scope;
                if (setting is VariableSetting variable)
                {
                    _ = VariableBindingCompiler.Compile(variable, target, solutionId, variablePorts, qualified, behavior);
                }
                else if (setting is ReferenceSetting reference &&
                    (configuration.IsExecutionFieldActive(fieldId) || BindingRelationCatalog.IndexVariables(reference.Reference.Selection).Any()))
                {
                    if (ReferenceOwnershipError(graph, item.Node, configuration, workflowId, fieldId, reference) is { } error)
                        throw new InvalidOperationException(error);
                    var compiled = new ParameterBindingService().CompileBinding(reference.Reference, target, qualified, reference.Conversion, behavior);
                    if (compiled.Compatibility is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved)
                        throw new InvalidOperationException(compiled.Diagnostic?.Message ?? "字段引用未就绪。");
                }
            }
        }
    }

    private static string? ReferenceOwnershipError(GraphState graph, NodeDefinition node,
        AdmittedToolConfiguration configuration, string scope, string fieldId, ReferenceSetting setting)
    {
        if (!string.IsNullOrEmpty(setting.RebindingReason)) return setting.RebindingReason;
        if (setting.OriginId is null) return null;
        const string prefix = "connection:";
        if (!setting.OriginId.StartsWith(prefix, StringComparison.Ordinal) || setting.OriginId.Length == prefix.Length)
            return "自动来源归属不是有效的连线标识。";
        var reference = setting.Reference;
        TypeRef? selected = reference.Type;
        foreach (var member in reference.MemberFieldPath)
        {
            if (selected?.Shape != ValueShape.Record) { selected = null; break; }
            selected = selected.Fields.SingleOrDefault(field => field.Id == member)?.Type;
        }
        if (configuration.Schema.WorkflowImageInputId != fieldId || selected != TypeRef.ResourceOf(ResourceKind.Image) ||
            reference.Selection is not BindingSelection.AllItems || setting.Conversion != BindingConversion.Automatic ||
            (reference.ScopeId ?? scope) != scope)
            return "自动图像来源的字段、引用或工作流归属不一致。";
        var edges = graph.Edges.Where(edge => edge.Edge.Id == setting.OriginId[prefix.Length..]).ToArray();
        return edges.Length == 1 && edges[0].TargetNodeId == node.Id && edges[0].SourceNodeId == reference.NodeId &&
            graph.Nodes.Any(source => source.Node.Id == reference.NodeId)
            ? null : "自动图像来源对应的连线不存在或不属于当前来源与目标节点。";
    }
}
