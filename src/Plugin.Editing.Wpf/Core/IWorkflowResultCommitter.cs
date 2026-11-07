using SunEyeVision.Plugin.Editing.Wpf.Execution.Results;

namespace SunEyeVision.Plugin.Editing.Wpf.Core;

/// <summary>
/// Commits explicitly collected data after a run. The tool receives an immutable
/// parameter snapshot; only the owning workflow node may mutate design-time state.
/// </summary>
public interface IWorkflowResultCommitter
{
    bool TryCommit(ToolResults result, out string? error);
}
