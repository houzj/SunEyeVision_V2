using System.Windows;
using System.Windows.Threading;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
 private string? SearchNavigateId(string query)
 {
  if(_viewModel==null||!_viewModel.CanEditWorkflowCanvas)return "当前状态不允许定位，请先停止运行或关闭参数窗口。";
  var nodes=_viewModel.FindWorkflowNodesById(query);
  var roots=_viewModel.GetWorkflowLocations().Where(entry=>entry.Root.Id==query.Trim()).ToArray();
  if(nodes.Count+roots.Length==0)return "未找到该ID，请输入完整节点序号或稳定ID。";
  if(nodes.Count+roots.Length>1)return "该ID对应多个对象，请改用名称搜索并核对完整路径。";
  var success=nodes.Count==1?SearchNavigate(nodes[0]):_viewModel.NavigateWorkflowLocation(roots[0]);
  return success?null:"目标已变化或暂不可定位，请重新搜索。";
 }
 private bool SearchNavigate(WorkflowSearchEntry entry)
 {
  if(_viewModel?.NavigateSearchResult(entry)!=true)return false;
  var tab=_viewModel.WorkflowTabViewModel.SelectedTab;
  Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>
  {
   if(!IsLoaded||!ReferenceEquals(_viewModel?.WorkflowTabViewModel.SelectedTab,tab)||tab?.CurrentWorkflowId!=entry.Container.Id||!ReferenceEquals(_viewModel.SelectedNode,entry.Node)||!tab.WorkflowNodes.Any(projected=>ReferenceEquals(projected.Definition,entry.Node)))return;
   var viewer=GetCurrentScrollViewer();if(viewer==null)return;
   viewer.UpdateLayout();var node=entry.Node;
   if(double.IsFinite(node.Position.X)&&double.IsFinite(node.Position.Y)&&double.IsFinite(node.Width)&&double.IsFinite(node.Height))
   {viewer.ScrollToHorizontalOffset(Math.Clamp(node.Position.X*tab.CurrentScale-viewer.ViewportWidth/2,0,viewer.ScrollableWidth));viewer.ScrollToVerticalOffset(Math.Clamp(node.Position.Y*tab.CurrentScale-viewer.ViewportHeight/2,0,viewer.ScrollableHeight));ScheduleMiniMapRedraw();}
  }));return true;
 }
}
