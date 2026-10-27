using System.Windows;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Features.WorkflowUI.Commands;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private long _rootOverviewRevision;
 private string _rootOverviewStamp="";
 internal (string Identity,long Revision,RootWorkflowOverviewEntry[] Entries) CaptureRootOverview()
 {
  var session=DocumentManager.Session;var document=DocumentManager.CurrentSolution;
  var identity=session?.Editing.SessionId.ToString()??"no-document";
  var roots=document?.Workflows.Where(w=>!w.IsSubroutine).ToArray()??Array.Empty<SunEyeVision.Workflow.Workflow>();
  var stamp=identity+"|"+(session?.Editing.Host.Capture().Version.ToString()??"")+"|"+string.Join("|",roots.Select(r=>r.Id+":"+r.Name+":"+r.WorkspaceSettings+":"+string.Join(",",r.Nodes.Select(n=>n.Id))));
  if(stamp!=_rootOverviewStamp){_rootOverviewStamp=stamp;_rootOverviewRevision++;}
  var entries=roots.Select(root=>
  {
   var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==root.Id);
   var snapshot=_executionManager.GetExecutionSnapshot(root.Id);bool running=_executionManager.IsRunning(root.Id);
   bool continuous=tab?.IsRunning==true&&tab.RunMode==RunMode.Continuous;
   return new RootWorkflowOverviewEntry(root.Id,root.Name,tab?.IsRootConfigurationReady==true,running,continuous,
    tab!=null&&!tab.IsInSubroutine&&!IsRunning&&!_executionManager.IsAnyRunning()&&!IsDebugWindowOpen&&DocumentManager.Session?.Editing.HasPendingChanges!=true&&root.Nodes.Count>0,
    tab!=null&&!tab.IsInSubroutine&&CanEditRootWorkspace,tab!=null&&!tab.IsInSubroutine&&CanEditRootWorkspace,
    snapshot is {CompletedRunCount:>0}?snapshot.CompletedRunCount:null,snapshot?.EndedAt!=null?snapshot.Elapsed.TotalMilliseconds:null,root.WorkspaceSettings.OverviewX,root.WorkspaceSettings.OverviewY);
  }).ToArray();
  return(identity,_rootOverviewRevision,entries);
 }
 private WorkflowTabViewModel? ResolveOverviewRequest(string rootId,string identity,long revision,bool edit)
 {
  var current=CaptureRootOverview();if(current.Identity!=identity||current.Revision!=revision||edit&&!CanEditRootWorkspace)return null;
  var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==rootId);
  return tab!=null&&DocumentManager.CurrentSolution?.Workflows.Any(w=>w.Id==rootId&&!w.IsSubroutine&&ReferenceEquals(_workflowEngine.GetWorkflowById(rootId),w))==true?tab:null;
 }
 internal bool OpenOverviewRoot(RootOverviewRequestEventArgs request)
 {
  var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,false);if(tab==null)return false;
  tab.ResetNavigation();
  SetSelectedTab(tab);NavigateToWorkflowCanvas(tab.RootWorkflowId);return true;
 }
 internal void ConfigureOverviewRoot(RootOverviewRequestEventArgs request,Window? owner)
 {var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,true);if(tab!=null)OpenRootConfiguration(tab,owner);}
 internal async Task RunOverviewRoot(RootOverviewRequestEventArgs request)
 {
  var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,false);if(tab==null)return;
  if(request.RunKind==RootOverviewRunKind.ToggleContinuous&&_executionManager.IsRunning(tab.RootWorkflowId)){_executionManager.StopContinuousRun(tab);return;}
  if(!CanEditRootWorkspace||tab.IsInSubroutine||tab.RootWorkflowNodes.Count==0)return;
  SetSelectedTab(tab);
  if(request.RunKind==RootOverviewRunKind.Single)await ExecuteRunWorkflow();else WorkflowTabViewModel.ToggleContinuous(tab);
 }
 internal bool CommitOverviewPosition(RootOverviewPositionEventArgs request,bool copy)
 {
  var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,true);if(tab==null)return false;
  var point=RootOverviewGeometry.Position(request.X,request.Y);
  if(copy)return CopyRootWorkspace(tab,point);
  var root=ResolveEditableRoot(tab);if(root==null)return false;
  var settings=root.WorkspaceSettings with{OverviewX=point.X,OverviewY=point.Y};if(settings==root.WorkspaceSettings)return true;
  RootHistory.Execute(new RootOverviewPositionCommand(DocumentManager.CurrentSolution!,root,settings,()=>UpdateCurrentSolutionInfo()));return true;
 }
}