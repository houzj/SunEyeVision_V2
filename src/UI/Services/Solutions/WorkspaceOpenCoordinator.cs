using System;
using System.Threading.Tasks;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.UI.Services.Solutions;

public enum WorkspaceOpenStatus { ActivatedExisting, RejectedBeforeCommit, Opened, CommittedDegraded }
public sealed record WorkspaceOpenResult(WorkspaceOpenStatus Status, bool Committed, string? Error = null)
{
    public bool IsSuccessful => Status is WorkspaceOpenStatus.ActivatedExisting or WorkspaceOpenStatus.Opened;
}

/// <summary>Stages candidate validation, transition, publication and activation without claiming device rollback.</summary>
public sealed class WorkspaceOpenCoordinator
{
    public async Task<WorkspaceOpenResult> OpenAsync(SolutionManager manager, Func<Solution> readCandidate,
        Func<Task<bool>> prepare, Func<Solution, Task> activate)
    {
        var previous = manager.CurrentSolution;
        var committed = false;
        try
        {
            var candidate = readCandidate();
            if (!await prepare()) return new(WorkspaceOpenStatus.RejectedBeforeCommit, false);
            // Publication may raise subscribers: if one fails, the new session is already authoritative.
            try { manager.SetCurrentSolution(candidate, registerInCatalog: false); }
            finally { committed = ReferenceEquals(manager.CurrentSolution, candidate); }
            await activate(candidate);
            return new(WorkspaceOpenStatus.Opened, true);
        }
        catch (Exception ex)
        {
            committed |= !ReferenceEquals(previous, manager.CurrentSolution);
            return new(committed ? WorkspaceOpenStatus.CommittedDegraded : WorkspaceOpenStatus.RejectedBeforeCommit, committed, ex.Message);
        }
    }
}
