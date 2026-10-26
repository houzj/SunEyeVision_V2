namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    /// <summary>实际编辑入口共用：运行、参数窗口和方案切换/关闭准备均阻止图修改。</summary>
    public bool CanEditWorkflowCanvas
    {
        get
        {
            var tab=WorkflowTabViewModel?.SelectedTab;
            return tab!=null&&!IsRunning&&!IsDebugWindowOpen&&!tab.IsRunning
                &&_solutionTransitionLease==null&&!_transitionPreparationInProgress
                &&!_executionManager.IsRunning(tab.RootWorkflowId)&&!_executionManager.IsRunning(tab.CurrentWorkflowId);
        }
    }
}
