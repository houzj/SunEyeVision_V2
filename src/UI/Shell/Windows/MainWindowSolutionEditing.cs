using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Features.GlobalVariableManager;

namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private GlobalVariableManagerDialog[] CurrentVariableEditors() => Application.Current?.Windows
        .OfType<GlobalVariableManagerDialog>().Where(w => w.IsLoaded && w.DataContext is GlobalVariableManagerViewModel model &&
            ReferenceEquals(model.EditingDocument, DocumentManager.CurrentSolution)).ToArray() ?? Array.Empty<GlobalVariableManagerDialog>();

    private bool TryValidateSolutionEditors(out string error)
    {
        var nodes = DocumentManager.CurrentSolution?.Workflows.SelectMany(w => w.Nodes).Cast<object>().ToArray() ?? Array.Empty<object>();
        foreach (var window in CurrentVariableEditors())
            if (!window.TryValidatePendingEdits(out error)) return false;
        return ToolEditorCommit.TryValidateOpenEditors(null, out error, node => nodes.Any(n => ReferenceEquals(n, node)));
    }
    private bool TryCommitSolutionEditors(out string error)
    {
        // Validate all participants first; no invalid edit is cancelled or reset to an older value.
        if (!TryValidateSolutionEditors(out error)) return false;
        foreach (var window in CurrentVariableEditors())
            if (!window.TryCommitPendingEdits(out error)) return false;
        var nodes = DocumentManager.CurrentSolution?.Workflows.SelectMany(w => w.Nodes).Cast<object>().ToArray() ?? Array.Empty<object>();
        return ToolEditorCommit.TryCommitOpenEditors(null, out error, node => nodes.Any(n => ReferenceEquals(n, node)));
    }
}
