using System.Collections.Immutable;
using SunEyeVision.Domain.Workflows;

namespace SunEyeVision.Application;

public sealed record WorkflowNavigationChange(Guid EditSessionId, WorkflowDefinition Workflow, long NavigationRevision, CanvasPoint? OverviewPosition);

public sealed partial class DocumentSession
{
    private long _navigationRevision;
    public event EventHandler<WorkflowNavigationChange>? WorkspaceNavigationChanged;
    public long NavigationRevision { get { lock (_gate) return _navigationRevision; } }

    internal static bool SameWorkspaceRunSettings(RootWorkspaceSettings first, RootWorkspaceSettings second)
        => first.RunIntervalMs == second.RunIntervalMs && first.StopOnNg == second.StopOnNg;
    internal static RootWorkspaceSettings MergeWorkspaceRunSettings(RootWorkspaceSettings run, RootWorkspaceSettings navigation)
        => navigation with { RunIntervalMs = run.RunIntervalMs, StopOnNg = run.StopOnNg };

    private static ImmutableArray<WorkflowChange> ProjectWorkflowConfigurationChanges(EditState before, EditState after)
    {
        var changes = ImmutableArray.CreateBuilder<WorkflowChange>();
        foreach (var (flow, root) in after.Roots)
        {
            var aspects = (WorkflowChangedAspect)0;
            if (before.Roots[flow] != root) aspects |= WorkflowChangedAspect.RootConfiguration;
            if (!SameWorkspaceRunSettings(before.Workspaces[flow], after.Workspaces[flow])) aspects |= WorkflowChangedAspect.RunSettings;
            if (aspects != 0) changes.Add(new(flow, aspects));
        }
        return changes.ToImmutable();
    }

    /// <summary>Navigation is owner-confined, persisted, and independent of parameter history and execution.</summary>
    public void SetOverviewPosition(WorkflowDefinition workflow, CanvasPoint? position)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        WorkflowNavigationChange change;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope = OpenScope(workflow.Id);
            if (!ReferenceEquals(scope.Flow, workflow) || workflow.IsSubroutine)
                throw new InvalidOperationException("总览导航不属于当前实际根流程。");
            var current = workflow.WorkspaceSettings;
            var next = current with { OverviewX = position?.X, OverviewY = position?.Y };
            next.Validate();
            if (next == current) return;
            var revision = checked(_navigationRevision + 1);
            change = new(EditSessionId, workflow, revision, position);
            workflow.WorkspaceSettings = next; _navigationRevision = revision;
        }
        var failures = new List<Exception>();
        _notifying = true;
        try
        {
            foreach (EventHandler<WorkflowNavigationChange> handler in WorkspaceNavigationChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { handler(this, change); } catch (Exception error) { failures.Add(error); }
        }
        finally { _notifying = false; }
        LastObserverError = failures.Count == 0 ? null : new AggregateException("总览导航观察者失败。", failures);
    }
}