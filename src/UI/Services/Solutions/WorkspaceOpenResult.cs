using System;
using System.Threading.Tasks;



namespace SunEyeVision.UI.Services.Solutions;

public enum WorkspaceOpenStatus { ActivatedExisting, RejectedBeforeCommit, Opened, CommittedDegraded }
public sealed record WorkspaceOpenResult(WorkspaceOpenStatus Status, bool Committed, string? Error = null)
{
    public bool IsSuccessful => Status is WorkspaceOpenStatus.ActivatedExisting or WorkspaceOpenStatus.Opened;
}

