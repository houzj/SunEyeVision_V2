using System;
using System.Linq;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private bool TryValidateSolutionEditors(out string error) => TryUseDocumentEditors(accept: false, out error);
    private bool TryCommitSolutionEditors(out string error) => TryUseDocumentEditors(accept: true, out error);

    private bool TryUseDocumentEditors(bool accept, out string error)
    {
        try
        {
            var session = DocumentManager.Session ?? throw new InvalidOperationException("当前没有方案编辑会话。");
            var nodes = session.Document.Workflows.SelectMany(w => w.Nodes).Cast<object>().ToArray();
            // Compatibility guard only. Registered draft scopes are prepared by the document,
            // including hidden/unloaded views, never rediscovered from the visual tree.
            if (!ToolEditorCommit.TryPrepareOpenDraftEditors(
                node => nodes.Any(n => ReferenceEquals(n, node)), out _, out error, includeDrafts: false)) return false;
            if (accept)
            {
                var publication = session.Editing.AcceptAll();
                if (publication is not null)
                    try { RefreshProductionPublication(session, publication); }
                    catch (Exception ex) { LogWarning("配置已接受，界面刷新未完成：" + ex.Message); }
                if (session.Editing.LastRefreshError is { } refreshError)
                    LogWarning("配置已接受，部分编辑器刷新未完成：" + refreshError);
            }
            else session.Editing.ValidateAll();
            error = "";
            return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }
}
