namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
    /// <summary>
    /// 画布 Ctrl+F 与空白右键"模块搜索"的宿主入口（VM ItemsPage.cs:444-446、DiagramPage.xaml:113
    /// 均打开模块搜索）。本方案复用已有 WorkflowSearchControl，不新造搜索面。
    /// </summary>
    public event Action? WorkflowSearchRequested;

    internal void RequestWorkflowSearch() => WorkflowSearchRequested?.Invoke();
}
