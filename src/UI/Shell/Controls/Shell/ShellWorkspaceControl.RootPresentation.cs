using System.Globalization;
using System.Windows;
using System.Windows.Threading;
namespace SunEyeVision.UI.Shell.Controls.Shell;
public partial class ShellWorkspaceControl
{
 private bool _rootOverviewActive;private string _overviewIdentity="";private long _overviewRevision=-1;private RootWorkflowOverviewEntry[] _overviewEntries=Array.Empty<RootWorkflowOverviewEntry>();
 private readonly DispatcherTimer _rootPresentationTimer=new(){Interval=TimeSpan.FromMilliseconds(200)};
 private void InitializeRootWorkspacePresentation()
 {
  _rootPresentationTimer.Tick+=(_,_)=>RefreshRootWorkspacePresentation();
  Loaded+=(_,_)=>{RefreshRootWorkspacePresentation();_rootPresentationTimer.Start();};
  Unloaded+=(_,_)=>{_rootPresentationTimer.Stop();RootOverview.CancelGesture();};
  RootOverview.DragFailed+=message=>{if(_viewModel!=null)_viewModel.StatusText=message;};
  RootOverview.OpenRequested+=(_,request)=>{if(_viewModel?.OpenOverviewRoot(request)==true)SetOverviewVisible(false);};
  RootOverview.ConfigureRequested+=(_,request)=>_viewModel?.ConfigureOverviewRoot(request,Window.GetWindow(this));
  RootOverview.RunRequested+=async(_,request)=>{try{if(_viewModel!=null)await _viewModel.RunOverviewRoot(request);}catch(Exception error){if(_viewModel!=null)_viewModel.StatusText="总览运行失败："+error.Message;}};
  RootOverview.MoveCommitted+=(_,request)=>CommitOverview(request,false);
  RootOverview.CopyRequested+=(_,request)=>CommitOverview(request,true);
 }
 private void RefreshRootWorkspacePresentation()
 {
  if(_viewModel==null)return;
  if(_rootOverviewActive)
  {
   var frame=_viewModel.CaptureRootOverview();
   if(frame.Identity!=_overviewIdentity||frame.Revision!=_overviewRevision||!frame.Entries.SequenceEqual(_overviewEntries))
   {RootOverview.SetEntries(frame.Entries,frame.Identity,frame.Revision);_overviewIdentity=frame.Identity;_overviewRevision=frame.Revision;_overviewEntries=frame.Entries;}
  }
  var tab=_viewModel.WorkflowTabViewModel.SelectedTab;
  WorkspaceStatusBar.ConfigurationState=tab==null||tab.RootWorkflowNodes.Count==0?WorkspaceConfigurationState.Waiting:tab.IsRootConfigurationReady?WorkspaceConfigurationState.Ready:WorkspaceConfigurationState.Incomplete;
  var selected=_viewModel.SelectedNode;
  var node=selected!=null&&tab?.RootWorkflowNodes.Any(n=>ReferenceEquals(n,selected))==true?_viewModel.StatusBar.ExecutionSnapshot?.Nodes.FirstOrDefault(n=>n.NodeId==selected.Id):null;
  WorkspaceStatusBar.ModuleTimeText=node is {HasModuleTiming:true}?node.ModuleElapsed.TotalMilliseconds.ToString("F2",CultureInfo.InvariantCulture):"—";
  WorkspaceStatusBar.AlgorithmTimeText=node is {HasAlgorithmTiming:true}?node.AlgorithmElapsedMs.ToString("F2",CultureInfo.InvariantCulture):"—";
 }
 private void CommitOverview(RootOverviewPositionEventArgs request,bool copy)
 {try{if(_viewModel?.CommitOverviewPosition(request,copy)!=true&&_viewModel!=null)_viewModel.StatusText="流程或编辑状态已变化，总览操作未提交。";}catch(Exception error){if(_viewModel!=null)_viewModel.StatusText="总览操作失败："+error.Message;}RefreshRootWorkspacePresentation();}
 private void ToggleRootOverviewClick(object sender,RoutedEventArgs e)=>SetOverviewVisible(!_rootOverviewActive);
 internal void SetOverviewVisible(bool visible)
 {
  _rootOverviewActive=visible;RootOverview.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
  WorkflowSearch.Visibility=visible?Visibility.Collapsed:Visibility.Visible;MiniMapHost.Visibility=visible?Visibility.Collapsed:Visibility.Visible;
  if(!visible)RootOverview.CancelGesture();RefreshRootWorkspacePresentation();
 }}