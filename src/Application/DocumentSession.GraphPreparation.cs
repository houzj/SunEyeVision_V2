using System.Text.Json;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

internal sealed record PreparedGraphEdit(
    IReadOnlyDictionary<WorkflowDefinition, GraphState> Before,
    IReadOnlyDictionary<WorkflowDefinition, GraphState> After,
    IReadOnlyList<GlobalVariable> ActualVariables,
    IReadOnlyList<GlobalVariable> VariablesBefore, IReadOnlyList<GlobalVariable> VariablesAfter,
    IReadOnlyList<ScopedNodeIdentity> Invalidated);

public sealed partial class DocumentSession
{
    // This prepares a single complete candidate only. History installation and notification
    // must still use the session's one publication transaction, not call GraphState.Apply here.
    private PreparedGraphEdit PrepareGraphEditCore(WorkflowScope scope, GraphEditDraft draft)
    {
        EnsureOwnerCanEdit(); ValidateScopeCore(scope);
        if (!ReferenceEquals(draft.Workflow, scope.Flow) || draft.OpeningHistoryRevision != History.Revision ||
            !draft.Opening.MatchesCurrent(scope.Flow))
            throw new InvalidOperationException("图编辑草稿已过期或不属于当前流程。");
        _preparing = true;
        try
        {
            draft.Seal();
            var flows = Document.Workflows.ToArray();
            var before = flows.ToDictionary(flow => flow, GraphState.Capture,
                (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
            var variables = Document.GlobalVariables.ToArray();
            var variablesBefore = GlobalVariable.CaptureDefinitions(variables).ToArray();
            var revision = History.Revision;
            bool Current()
            {
                ValidateScopeCore(scope);
                if (History.Revision != revision || Document.Workflows.Count != flows.Length ||
                    flows.Where((flow, index) => !ReferenceEquals(Document.Workflows[index], flow)).Any() ||
                    before.Any(pair => !pair.Value.MatchesCurrent(pair.Key)) ||
                    Document.GlobalVariables.Count != variables.Length ||
                    variables.Where((variable, index) => !ReferenceEquals(Document.GlobalVariables[index], variable)).Any())
                    return false;
                var now = GlobalVariable.CaptureDefinitions(variables);
                return now.Select(VariableConfigurationSignature).SequenceEqual(variablesBefore.Select(VariableConfigurationSignature));
            }
            var beforeDependencies = GraphDependencySnapshot.Create(Document, before, variablesBefore, AdmitConfiguration);
            var proposed = new Dictionary<WorkflowDefinition, GraphState>(before,
                (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance)
            {
                [scope.Flow] = GraphState.FromDraft(draft, PrepareConnectionBindings(draft))
            };
            var reconciled = GraphBindingReconciliation.Prepare(Document, proposed, variablesBefore, AdmitConfiguration, Current);
            var afterDependencies = GraphDependencySnapshot.Create(Document, reconciled.Graphs, reconciled.Variables, AdmitConfiguration);
            if (!Current()) throw new InvalidOperationException("图候选准备期间正式方案已变化。");
            var beforeById = before.ToDictionary(pair => pair.Key.Id, pair => pair.Value, StringComparer.Ordinal);
            var afterById = reconciled.Graphs.ToDictionary(pair => pair.Key.Id, pair => pair.Value, StringComparer.Ordinal);
            var changedVariableIds = variablesBefore.Zip(reconciled.Variables)
                .Where(pair => VariableConfigurationSignature(pair.First) != VariableConfigurationSignature(pair.Second))
                .Select(pair => pair.First.Id).ToHashSet(StringComparer.Ordinal);
            var seeds = new HashSet<ScopedNodeIdentity>(reconciled.Invalidated);
            if (changedVariableIds.Count != 0)
                foreach (var graph in new[] { before, reconciled.Graphs })
                    foreach (var (flow, state) in graph)
                        foreach (var item in state.Nodes)
                            if (BindingRelationCatalog.VariableReferences(AdmitConfiguration(item.Node, item.Configuration))
                                .Any(reference => reference.Reference.SolutionId == _documentId &&
                                    changedVariableIds.Contains(reference.Reference.VariableId)))
                                seeds.Add(new(flow.Id, item.Node.Id));
            var invalidated = GraphInvalidationProjection.Compare(beforeById, afterById, beforeDependencies,
                afterDependencies, seeds,
                (node, configuration) => AdmitConfiguration(node, configuration).ExecutionConfigurationIdentity);
            if (!Current()) throw new InvalidOperationException("图依赖校验期间正式方案已变化。");
            var historyBefore = before.ToDictionary(pair => pair.Key, pair => pair.Value.WithoutCurrentValues(),
                (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
            var historyAfter = reconciled.Graphs.ToDictionary(pair => pair.Key, pair => pair.Value.WithoutCurrentValues(),
                (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
            return new PreparedGraphEdit(historyBefore, historyAfter, Array.AsReadOnly(variables),
                StripRuntimeValues(variablesBefore), StripRuntimeValues(reconciled.Variables), invalidated);
        }
        finally { _preparing = false; }
    }

    private static string VariableConfigurationSignature(GlobalVariable variable)
    {
        // Execution values/revisions are deliberately excluded: they are retained from the live
        // variable on installation and are not restored by undo/redo.
        return JsonSerializer.Serialize(new
        {
            variable.Id, variable.Name, variable.Group, variable.Index, variable.Type, variable.Description,
            variable.ProducerBinding, variable.RebindingReason, variable.BindingValidationPending, variable.AdditionalData
        });
    }

    // A root rename refreshes producer display metadata without changing execution identity.
    private static string VariableInvalidationSignature(GlobalVariable variable) => JsonSerializer.Serialize(new
    {
        variable.Id, variable.Name, variable.Group, variable.Index, variable.Type, variable.Description,
        Producer = variable.ProducerBinding is { } producer ? new
        { producer.ReferenceExpression, producer.NodeName, producer.ResultDisplayName } : null,
        variable.RebindingReason, variable.BindingValidationPending, variable.AdditionalData
    });
    private static IReadOnlyList<GlobalVariable> StripRuntimeValues(IReadOnlyList<GlobalVariable> source)
    {
        var copies = GlobalVariable.CaptureDefinitions(source.ToArray()).ToArray();
        lock (GlobalVariable.ValueGate)
            foreach (var variable in copies) variable.ApplyValueRaw(new GlobalVariable.ValueState(null, 0));
        return Array.AsReadOnly(copies);
    }
}
