using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
 private bool SearchNavigate(WorkflowSearchEntry entry)
 {
  if(_viewModel?.NavigateSearchResult(entry)!=true)return false;
  var tab=_viewModel.WorkflowTabViewModel.SelectedTab;
  Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
  {
   if(!IsLoaded||!ReferenceEquals(_viewModel?.WorkflowTabViewModel.SelectedTab,tab)||tab?.CurrentWorkflowId!=entry.Container.Id||!ReferenceEquals(_viewModel.SelectedNode,entry.Node)||!tab.WorkflowNodes.Contains(entry.Node))return;
   var viewer=GetCurrentScrollViewer();if(viewer==null)return;
   viewer.UpdateLayout();var node=entry.Node;
   if(double.IsFinite(node.Position.X)&&double.IsFinite(node.Position.Y)&&double.IsFinite(node.Width)&&double.IsFinite(node.Height))
   {viewer.ScrollToHorizontalOffset(Math.Clamp((node.Position.X+node.Width/2)*tab.CurrentScale-viewer.ViewportWidth/2,0,viewer.ScrollableWidth));viewer.ScrollToVerticalOffset(Math.Clamp((node.Position.Y+node.Height/2)*tab.CurrentScale-viewer.ViewportHeight/2,0,viewer.ScrollableHeight));ScheduleMiniMapRedraw();}
  }));return true;
 }
}
