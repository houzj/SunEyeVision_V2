using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
    private readonly SunEyeVision.UI.Services.Editing.ParameterWindowRegistry _auxiliaryParameterWindows=new();
    public IDisposable TrackParameterWindow(System.Windows.Window window)
    {
        _auxiliaryParameterWindows.Changed-=AuxiliaryParameterWindowsChanged;
        _auxiliaryParameterWindows.Changed+=AuxiliaryParameterWindowsChanged;
        return _auxiliaryParameterWindows.Track(window);
    }
    private void AuxiliaryParameterWindowsChanged(object? sender,EventArgs e)
    {
        OnPropertyChanged(nameof(IsDebugWindowOpen));OnPropertyChanged(nameof(CanEditWorkflowCanvas));OnPropertyChanged(nameof(CanEditRootWorkspace));
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
    private async Task<bool> TryCloseParameterWindowsForTransitionAsync()
    {
        if(_openDebugWindow is {} previous)
        {
            if(previous is Services.Editing.RuntimeToolEditorWindow actual)
            {if(!await actual.CloseAndDrainAsync()){EndSolutionTransition();return false;}}
            else {previous.Close();if(previous.IsVisible){EndSolutionTransition();return false;}}
        }
        if(_auxiliaryParameterWindows.TryCloseAll())return true;
        EndSolutionTransition();return false;
    }
    /// <summary>实际编辑入口共用：运行、参数窗口和方案切换/关闭准备均阻止图修改。</summary>
    public bool CanEditWorkflowCanvas
    {
        get
        {
            var tab=WorkflowTabViewModel?.SelectedTab;
            return tab!=null&&!IsRunning&&!IsDebugWindowOpen&&!tab.IsRunning
                &&!_solutionTransitionPrepared&&!_transitionPreparationInProgress
                &&!IsFormalRootRunning(tab.RootWorkflowId);
        }
    }

}
