using System.Windows;
using SunEyeVision.UI.Shell.Controls.Shell;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.Domain.Workflows;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private long _rootOverviewRevision;
 private string _rootOverviewStamp="";
 internal (string Identity,long Revision,RootWorkflowOverviewEntry[] Entries) CaptureRootOverview()
 {
  var session=DocumentManager.CurrentSession;var document=session?.Document;
  var identity=session?.Editing.SessionId.ToString()??"no-document";
  var roots=document?.Workflows.Where(w=>!w.IsSubroutine).ToArray()??Array.Empty<WorkflowDefinition>();
  var stamp=identity+"|"+(session?.ChangeRevision.ToString()??"")+"/"+(session?.NavigationRevision.ToString()??"")+"|"+string.Join("|",roots.Select(r=>r.Id+":"+r.Name+":"+r.WorkspaceSettings+":"+string.Join(",",r.Nodes.Select(n=>n.Id))));
  if(stamp!=_rootOverviewStamp){_rootOverviewStamp=stamp;_rootOverviewRevision++;}
  var entries=roots.Select(root=>
  {
   var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==root.Id);
   var snapshot=GetFormalExecutionSnapshot(root.Id);bool running=_formalRunningRootIds.Contains(root.Id);
   bool continuous=tab?.IsRunning==true&&tab.RunMode==RunMode.Continuous;
   return new RootWorkflowOverviewEntry(root.Id,root.Name,tab?.IsRootConfigurationReady==true,running,continuous,
    tab!=null&&!tab.IsInSubroutine&&!IsRunning&&!IsDebugWindowOpen&&DocumentManager.CurrentSession?.Editing.HasPendingChanges!=true&&root.Nodes.Count>0,
    tab!=null&&!tab.IsInSubroutine&&CanEditRootWorkspace,tab!=null&&!tab.IsInSubroutine&&CanEditRootCollection,
    snapshot is {CompletedRunCount:>0}?snapshot.CompletedRunCount:null,snapshot?.EndedAt!=null?snapshot.Elapsed.TotalMilliseconds:null,root.WorkspaceSettings.OverviewX,root.WorkspaceSettings.OverviewY);
  }).ToArray();
  return(identity,_rootOverviewRevision,entries);
 }
 private WorkflowTabViewModel? ResolveOverviewRequest(string rootId,string identity,long revision,bool edit)
 {
  var current=CaptureRootOverview();if(current.Identity!=identity||current.Revision!=revision||edit&&!CanEditRootCollection)return null;
  var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(t=>t.RootWorkflowId==rootId);
  return tab!=null&&DocumentManager.CurrentSession?.GetWorkflows().Any(w=>w.Id==rootId&&!w.IsSubroutine)==true?tab:null;
 }
 internal bool OpenOverviewRoot(RootOverviewRequestEventArgs request)
 {
  var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,false);if(tab==null)return false;
  SetSelectedTab(tab);NavigateToWorkflowCanvas(tab.RootWorkflowId);return true;
 }
 internal void ConfigureOverviewRoot(RootOverviewRequestEventArgs request,Window? owner)
 {var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,true);if(tab!=null)OpenRootConfiguration(tab,owner);}
 internal async Task RunOverviewRoot(RootOverviewRequestEventArgs request)
 {
  var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,false);if(tab==null)return;
  if(request.RunKind==RootOverviewRunKind.ToggleContinuous&&IsFormalRootRunning(tab.RootWorkflowId)){RequestStopFormalExecution();return;}
  if(!CanEditRootWorkspace||tab.IsInSubroutine||tab.RootWorkflowNodes.Count==0)return;
  SetSelectedTab(tab);
  if(request.RunKind==RootOverviewRunKind.Single)await ExecuteRunWorkflow();else StartFormalContinuous(new[]{tab.RootWorkflowId});
 }
 internal bool CommitOverviewPosition(RootOverviewPositionEventArgs request,bool copy)
 {
  var tab=ResolveOverviewRequest(request.RootId,request.DocumentIdentity,request.Revision,copy);if(tab==null)return false;
  var point=RootOverviewGeometry.Position(request.X,request.Y);
  if(copy)return CopyRootWorkspace(tab,point);
  var session=DocumentManager.CurrentSession;
  var root=session?.GetWorkflows().SingleOrDefault(flow=>flow.Id==tab.RootWorkflowId&&!flow.IsSubroutine);
  if(session is null||root is null||!CanAcceptExternalActivation)return false;
  session.SetOverviewPosition(root,new CanvasPoint(point.X,point.Y));UpdateCurrentSolutionInfo();return true;
 }
}
