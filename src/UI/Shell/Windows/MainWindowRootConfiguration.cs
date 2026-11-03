using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services.Editing;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    private RootWorkflowConfigurationWindow? _rootConfigurationWindow;

    internal RootWorkflowConfigurationWindow? OpenRootConfiguration(WorkflowTabViewModel tab, Window? owner)
    {
        var session = DocumentManager.CurrentSession;
        if (session is null) return null;
        if (_rootConfigurationWindow is { IsVisible: true } existing)
        {
            if (existing.Context.Root.Id == tab.RootWorkflowId && existing.Context.IsValid)
            { existing.Activate(); return existing; }
            if (existing.Context.HasPendingChanges)
            { StatusText = "请先应用或取消当前流程配置。"; return null; }
            existing.Close();
            if (_rootConfigurationWindow is { IsVisible: true }) return null;
        }

        var root = session.GetWorkflows().SingleOrDefault(item => item.Id == tab.RootWorkflowId && !item.IsSubroutine);
        if (root is null || !CanEditRootWorkspace) return null;
        bool Current() => ReferenceEquals(DocumentManager.CurrentSession, session) &&
            session.GetWorkflows().Any(item => ReferenceEquals(item, root));
        bool Editable() => Current() && !IsRunning && _openDebugWindow?.IsLoaded != true && _auxiliaryParameterWindows.Count <= 1;
        var context = new RootWorkflowConfigurationContext(session, root, Current, Editable,
            _ => StatusText = "流程配置已应用。");
        RootWorkflowConfigurationWindow window;
        try
        {
            // 运行与结果读取使用尚未交付的会话执行入口；窗口自身显示禁用状态。
            window = new(context, run: null, repeat: null, stop: null, running: null) { Owner = owner };
        }
        catch { context.Dispose(); throw; }
        _rootConfigurationWindow = window;
        context.Changed+=UpdateCurrentSolutionInfo;
        var registration = TrackParameterWindow(window);
        window.Closed += (_, _) =>
        {
            registration.Dispose();context.Changed-=UpdateCurrentSolutionInfo;
            if (ReferenceEquals(_rootConfigurationWindow, window)) _rootConfigurationWindow = null;
        };
        try { window.Show(); return window; }
        catch { registration.Dispose(); context.Dispose(); _rootConfigurationWindow = null; throw; }
    }
}

