using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Features.WorkflowUI;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
    private int _viewRestoreGeneration;
    private bool _restoringContainerView;
    private void CaptureWorkspaceView(WorkflowTabViewModel? tab)
    {
        if (tab==null||_restoringContainerView) return;
        var viewer=_canvasScrollViewer??GetCurrentScrollViewer();
        if(viewer!=null)tab.CaptureViewport(viewer.HorizontalOffset,viewer.VerticalOffset);
    }
    private void WorkspaceContentChanging(object? sender,EventArgs e)
    {
        CaptureWorkspaceView(sender as WorkflowTabViewModel);
        ++_viewRestoreGeneration;_restoringContainerView=true;
    }
    private static void RestoreSavedViewport(WorkflowTabViewModel tab,System.Windows.Controls.ScrollViewer viewer)
    {
        viewer.ScrollToHorizontalOffset(tab.ViewportCanvasOrigin.X*tab.CurrentScale);
        viewer.ScrollToVerticalOffset(tab.ViewportCanvasOrigin.Y*tab.CurrentScale);
    }
    private void QueueRestoreWorkspaceView()
    {
        var tab=_viewModel?.WorkflowTabViewModel?.SelectedTab;
        if(tab==null)return;
        var nodes=tab.WorkflowNodes;var edges=tab.WorkflowConnections;
        var generation=++_viewRestoreGeneration;_restoringContainerView=true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
        {
            if(generation!=_viewRestoreGeneration)return;
            try
            {
                if(!ReferenceEquals(_viewModel?.WorkflowTabViewModel?.SelectedTab,tab)||!ReferenceEquals(nodes,tab.WorkflowNodes)||!ReferenceEquals(edges,tab.WorkflowConnections))return;
                var viewer=_canvasScrollViewer??GetCurrentScrollViewer();
                ApplyZoom(tab.CurrentScale,tab.CurrentScale);
                UpdateCanvasSizingGridSize();viewer?.UpdateLayout();
                if(tab.HasSavedViewport&&viewer!=null)
                {
                    RestoreSavedViewport(tab,viewer);
                }
                else CenterOnWorkflowNodes();
                UpdateZoomDisplay();ScheduleMiniMapRedraw();
            }
            finally {_restoringContainerView=false;}
        }));
    }
}
