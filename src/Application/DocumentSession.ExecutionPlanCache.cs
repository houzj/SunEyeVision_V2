using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Called while the document gate is held. Runtime current values do not invalidate the plan.</summary>
    private ExecutionPlan GetOrBuildExecutionPlan(IReadOnlyList<WorkflowDefinition> flows,
        IReadOnlyList<GlobalVariable> definitions)
    {
        if (_executionPlan is { } cached && cached.Generation == _executionPlanGeneration && cached.Matches(Document) &&
            _executionServer.TryUseResidentPlan(_documentId, _executionPlanGeneration))
        {
            // Keep the VM-like plan structure resident, but still validate the live tool
            // declarations on every admission. This preserves plugin retirement/race
            // semantics without rebuilding GraphState and dependency edges.
            foreach (var flow in flows)
                foreach (var node in flow.Nodes)
                    _ = AdmitConfiguration(node, node.Configuration);
            return cached;
        }

        var states = flows.ToDictionary(flow => flow, GraphState.Capture,
            (IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
        var dependencies = GraphDependencySnapshot.Create(Document, states, definitions, AdmitConfiguration);
        var plans = flows.Select(flow => new ExecutionWorkflowPlan(flow, states[flow])).ToArray();
        var plan = new ExecutionPlan(EditSessionId, _documentId, _executionPlanGeneration, plans, dependencies);
        _executionPlan = plan;
        _executionServer.PublishResidentPlan(_documentId, _executionPlanGeneration);
        return plan;
    }

    private void InvalidateExecutionPlan(DocumentChangeSet change)
    {
        var structural = !change.ChangedConnections.IsDefaultOrEmpty || !change.ChangedWorkflows.IsDefaultOrEmpty ||
            !change.ChangedRoots.IsDefaultOrEmpty || !change.ExecutionInvalidatedNodes.IsDefaultOrEmpty ||
            (!change.ChangedNodes.IsDefaultOrEmpty && change.ChangedNodes.Any(item =>
                (item.Aspects & (NodeChangedAspect.Identity | NodeChangedAspect.Configuration)) != 0));
        if (!structural) return;
        _executionPlanGeneration = checked(_executionPlanGeneration + 1);
        _executionPlan = null;
        _executionServer.InvalidateResidentPlan();
    }
}
