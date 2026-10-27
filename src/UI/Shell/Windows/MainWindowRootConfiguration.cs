using System.Windows;
using SunEyeVision.UI.Features.WorkflowUI;
using SunEyeVision.UI.Services.Editing;
using SunEyeVision.Workflow;
using Flow=SunEyeVision.Workflow.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 private RootWorkflowConfigurationWindow? _rootConfigurationWindow;
 internal RootWorkflowConfigurationWindow? OpenRootConfiguration(WorkflowTabViewModel tab,Window? owner)
 {
  var session=DocumentManager.Session;if(session==null)return null;
  if(_rootConfigurationWindow is {IsVisible:true} existing)
  {
   if(existing.Context.Root.Id==tab.RootWorkflowId&&existing.Context.IsValid){existing.Activate();return existing;}
   if(existing.Context.HasPendingChanges){StatusText="请先应用或取消当前流程配置。";return null;}existing.Close();
  }
  if(ResolveEditableRoot(tab) is not {} root||!CanEditRootWorkspace)return null;
  bool Current()=>ReferenceEquals(DocumentManager.Session,session)&&session.Document.Workflows.Any(w=>ReferenceEquals(w,root))&&ReferenceEquals(_workflowEngine.GetWorkflowById(root.Id),root);
  bool Editable()=>Current()&&!IsRunning&&!_executionManager.IsAnyRunning()&&_solutionTransitionLease==null&&!_transitionPreparationInProgress&&_openDebugWindow?.IsLoaded!=true&&_auxiliaryParameterWindows.Count<=1;
  var query=CreateDataSourceQueryService(root.Id);
  var context=new RootWorkflowConfigurationContext(session,root,query,Current,Editable,publication=>
  {
   RefreshProductionPublication(session,publication,query);
   foreach(var node in publication.InvalidatedNodes)_workflowContextService.Get(node.WorkflowId)?.RemoveNodeResult(node.NodeId);
   UpdateCurrentSolutionInfo();
  });
  RootWorkflowConfigurationWindow window;
  try
  {
   window=new(context,async()=>{if(!Editable())throw new InvalidOperationException("当前流程不可开始运行。");SetSelectedTab(tab);await ExecuteRunWorkflow();},
    ()=>{if(!Editable())throw new InvalidOperationException("当前流程不可连续运行。");SetSelectedTab(tab);WorkflowTabViewModel.ToggleContinuous(tab);},
    ()=>{if(Current())_executionManager.StopContinuousRun(tab);},()=>_executionManager.IsRunning(root.Id),
    id=>Current()?_workflowContextService.Get(root.Id)?.GetNodeResult(id)?.Result:null){Owner=owner};
  }
  catch{context.Dispose();throw;}
  _rootConfigurationWindow=window;var registration=TrackParameterWindow(window);
  window.Closed+=(_,_)=>{registration.Dispose();if(ReferenceEquals(_rootConfigurationWindow,window))_rootConfigurationWindow=null;};
  try{window.Show();return window;}catch{registration.Dispose();context.Dispose();_rootConfigurationWindow=null;throw;}
 }
}
