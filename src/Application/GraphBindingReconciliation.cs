using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

/// <summary>Prepares producer and field repairs in private graph/variable candidates.</summary>
internal static class GraphBindingReconciliation
{
    internal sealed record Prepared(IReadOnlyDictionary<WorkflowDefinition, GraphState> Graphs,
        IReadOnlyList<GlobalVariable> Variables, IReadOnlySet<ScopedNodeIdentity> Invalidated);

    internal static Prepared Prepare(SolutionDefinition document,
        IReadOnlyDictionary<WorkflowDefinition, GraphState> candidates,
        IReadOnlyList<GlobalVariable> candidateVariables,
        Func<NodeDefinition, ToolConfiguration, AdmittedToolConfiguration> admit, Func<bool> isCurrent)
    {
        var graphs = document.Workflows.ToDictionary(flow => flow,
            flow => candidates.TryGetValue(flow, out var graph) ? graph : GraphState.Capture(flow),
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
        var variables = GlobalVariable.CaptureDefinitions(candidateVariables.ToArray()).ToArray();
        var invalidated = new HashSet<ScopedNodeIdentity>();

        IReadOnlyList<NodeDefinition>? FindScope(string workflowId)
        {
            var matches = graphs.Where(pair => pair.Key.Id == workflowId).ToArray();
            return matches.Length == 1 ? Array.AsReadOnly(matches[0].Value.Nodes.Select(item => item.Node).ToArray()) : null;
        }
        ToolSchema? ReadContract(NodeDefinition node)
        {
            var matches = graphs.Values.SelectMany(graph => graph.Nodes)
                .Where(item => ReferenceEquals(item.Node, node)).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("生产节点不属于唯一候选流程。");
            return admit(node, matches[0].Configuration).Schema;
        }

        foreach (var variable in variables)
        {
            if (variable.ProducerBinding is not { } producer) continue;
            var validation = GlobalProducerBindingRules.Validate(producer, variable, FindScope, ReadContract, isCurrent);
            if (validation.State == BindingCompatibility.Unresolved)
            {
                variable.BindingValidationPending = validation.Reason;
                continue;
            }
            if (validation.State is BindingCompatibility.Compatible or BindingCompatibility.Conditional)
            {
                variable.BindingValidationPending = string.Empty;
                continue;
            }
            variable.ProducerBinding = null;
            variable.RebindingReason = validation.Reason;
            variable.BindingValidationPending = string.Empty;
            foreach (var flow in document.Workflows)
            {
                var graph = graphs[flow];
                var replacements = new Dictionary<NodeDefinition, ToolConfiguration>(ReferenceEqualityComparer.Instance);
                foreach (var item in graph.Nodes)
                {
                    var configuration = admit(item.Node, item.Configuration);
                    var changes = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
                    var affectedFields = BindingRelationCatalog.VariableReferences(configuration)
                        .Where(item => item.Reference.SolutionId == document.Id && item.Reference.VariableId == variable.Id)
                        .Select(item => item.FieldId).Distinct(StringComparer.Ordinal);
                    foreach (var fieldId in affectedFields)
                    {
                        const string reason = "全局变量输入来源已变化，请重新绑定";
                        var setting = configuration.Configuration.Fields.Read(fieldId);
                        if (setting is VariableSetting value) changes.Add(fieldId, value with { RebindingReason = reason });
                        else if (setting is ReferenceSetting reference) changes.Add(fieldId, reference with { RebindingReason = reason });
                        // Display-expression slots have no per-slot invalidation field. The variable
                        // retains its explicit rebinding reason and the containing node is invalidated.
                        invalidated.Add(new(flow.Id, item.Node.Id));
                    }
                    if (changes.Count == 0) continue;
                    replacements.Add(item.Node, configuration.WithFieldSettings(changes).Configuration);
                }
                if (replacements.Count != 0) graphs[flow] = graph.WithConfigurations(replacements);
            }
        }
        if (!isCurrent()) throw new InvalidOperationException("图或变量归属在候选校验期间变化。");
        return new Prepared(new System.Collections.ObjectModel.ReadOnlyDictionary<WorkflowDefinition, GraphState>(graphs),
            Array.AsReadOnly(variables), invalidated);
    }
}
