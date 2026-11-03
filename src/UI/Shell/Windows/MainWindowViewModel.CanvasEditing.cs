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
    private bool TryCloseParameterWindowsForTransition()
    {
        if(_openDebugWindow is {} previous)
        {
            var closed=false;void Closed(object? sender,EventArgs args)=>closed=true;previous.Closed+=Closed;
            try{previous.Close();}finally{previous.Closed-=Closed;}
            if(!closed){EndSolutionTransition();return false;}
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
