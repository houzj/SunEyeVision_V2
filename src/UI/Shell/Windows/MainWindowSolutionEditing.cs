using System;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.UI.Features.GlobalVariableManager;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private GlobalVariableManagerDialog[] CurrentVariableEditors() => Application.Current?.Windows
        .OfType<GlobalVariableManagerDialog>().Where(w => w.IsLoaded && w.DataContext is GlobalVariableManagerViewModel model &&
            ReferenceEquals(model.EditingDocument, DocumentManager.CurrentSolution)).ToArray() ?? Array.Empty<GlobalVariableManagerDialog>();

    private bool TryValidateSolutionEditors(out string error)
    {
        if (!TryPrepareSolutionEditors(out var contexts, out var windows, out error)) return false;
        if (contexts.Length == 0 && windows.Length == 0) return true;
        try
        {
            var host = _productionEditorHost ?? throw new InvalidOperationException("编辑事务宿主已失效。");
            host.ValidateBatch(contexts.Select(c => c.PreparedDraft).ToArray(),
                (windows.SingleOrDefault()?.DataContext as GlobalVariableManagerViewModel)?.Draft);
            error = ""; return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
    private bool TryCommitSolutionEditors(out string error)
    {
        if (!TryPrepareSolutionEditors(out var contexts, out var windows, out error)) return false;
        if (contexts.Length == 0 && windows.Length == 0) return true;
        BindingConfigurationPublication publication;
        var session = DocumentManager.Session;
        try
        {
            if (session is null || !ReferenceEquals(_productionEditorSession, session))
                throw new InvalidOperationException("编辑所属方案会话已变化，草稿保留。");
            var host = _productionEditorHost ?? throw new InvalidOperationException("编辑事务宿主已失效。");
            publication = host.CommitBatch(contexts.Select(c => c.PreparedDraft).ToArray(),
                (windows.SingleOrDefault()?.DataContext as GlobalVariableManagerViewModel)?.Draft);
        }
        catch (Exception ex) { error = ex.Message; return false; }
        // Acceptance is final. Refresh failures leave the accepted document dirty, never retry its writes.
        foreach (var context in contexts) context.AcceptPublication(publication);
        foreach (var window in windows) window.AcceptPublication();
        try { RefreshProductionPublication(session!, publication); }
        catch (Exception ex) { LogWarning("配置已提交，界面刷新未完成：" + ex.Message); }
        error = ""; return true;
    }
    private bool TryPrepareSolutionEditors(out ProductionEditorContext[] contexts,
        out GlobalVariableManagerDialog[] windows, out string error)
    {
        contexts = Array.Empty<ProductionEditorContext>();
        windows = CurrentVariableEditors();
        if (windows.Length > 1) { error = "同一方案存在多个变量草稿，请先保留一个变量编辑窗口。"; return false; }
        var nodes = DocumentManager.CurrentSolution?.Workflows.SelectMany(w => w.Nodes).Cast<object>().ToArray() ?? Array.Empty<object>();
        // This preflight detects legacy changes before flushing any source. Supported editors flush private sources only.
        if (!ToolEditorCommit.TryPrepareOpenDraftEditors(node => nodes.Any(n => ReferenceEquals(n, node)), out var prepared, out error)) return false;
        contexts = prepared.OfType<ProductionEditorContext>().ToArray();
        if (contexts.Length != prepared.Count || contexts.Any(c => !ReferenceEquals(c.Host, _productionEditorHost)))
        { error = "编辑器不属于当前文档的统一事务宿主。"; return false; }
        foreach (var window in windows)
        {
            if (window.DataContext is not GlobalVariableManagerViewModel model || !ReferenceEquals(model.Host, _productionEditorHost))
            { error = "变量草稿不属于当前文档事务。"; return false; }
            if (!window.TryCommitPendingEdits(out error)) return false;
        }
        error = ""; return true;
    }
}
