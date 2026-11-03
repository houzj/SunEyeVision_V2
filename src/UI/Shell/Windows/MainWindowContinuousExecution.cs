using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.UI.Shell.Windows;

public partial class MainWindowViewModel
{
 private DocumentContinuousRun? _formalContinuousRun;
 private long _continuousDisplayedRound, _continuousCompletedRound;
 private readonly Dictionary<string,long> _continuousRootRounds=new(StringComparer.Ordinal);
 private bool _singlePaused;
 private Exception? _formalReleaseError;
 private string? _continuousCompletionText;
 public bool IsContinuousRunning=>_formalContinuousRun is not null;
 public bool IsExecutionPaused=>_formalContinuousRun?.State.Phase==ContinuousPhase.Paused || _formalActiveBatch is not null&&_singlePaused;
 public bool IsExecutionStopping=>_formalContinuousRun?.State.Phase is ContinuousPhase.Stopping or ContinuousPhase.Completed;
 private string FormalExecutionStateText=>!IsRunning?"已停止":IsExecutionStopping?"正在停止并排空":IsExecutionPaused?"已暂停":_formalContinuousRun?.State.Phase==ContinuousPhase.Waiting?"等待下一轮":"运行中";
 private bool CanPauseFormalExecution()=>IsRunning&&!IsExecutionStopping&&(_formalContinuousRun is null||_formalContinuousRun.State.Phase!=ContinuousPhase.Created);
 private bool CanToggleContinuousAll()=>IsContinuousRunning?!IsExecutionStopping:!IsRunning&&_formalRunTask is null&&CanEditRootWorkspace&&WorkflowTabViewModel.Tabs.Any(tab=>tab.RootWorkflowNodes.Count>0);
 private void ToggleFormalPause()
 {
  try
  {
   if(_formalContinuousRun is {} continuous){if(IsExecutionPaused)continuous.Resume();else continuous.Pause();}
   else if(_formalActiveBatch is {} single){if(_singlePaused){if(single.Resume())_singlePaused=false;}else if(single.Pause())_singlePaused=true;}
   NotifyFormalRunState();
  }
  catch(Exception error){StatusText="暂停或恢复失败："+error.Message;}
 }
 private bool StartAllContinuousWorkflows()
 {
  var roots=DocumentManager.CurrentSession?.GetWorkflows().Where(flow=>!flow.IsSubroutine&&flow.Nodes.Count>0).Select(flow=>flow.Id).ToArray()??Array.Empty<string>();
  return StartFormalContinuous(roots);
 }
 private bool StartFormalContinuous(IReadOnlyList<string> roots)
 {
  try
  {
   if(_formalRunTask is not null||IsRunning||!CanEditRootWorkspace||IsDebugWindowOpen)throw new InvalidOperationException("请先停止并排空正式执行，处理参数窗口或方案过渡。");
   var session=DocumentManager.CurrentSession??throw new InvalidOperationException("当前没有方案。");
   if(roots.Count==0)throw new InvalidOperationException("没有非空根流程，不能启动连续执行。");
   if(!TryCommitSolutionEditors(out var error))throw new InvalidOperationException(error);
   foreach(var id in roots)
   {
    var root=session.GetWorkflows().Single(flow=>flow.Id==id&&!flow.IsSubroutine);
    if(root.Nodes.Count==0)throw new InvalidOperationException("根流程没有可执行节点："+root.Name);
    var issues=session.CheckWorkflowReadiness(id);if(issues.Count!=0)throw new InvalidOperationException(root.Name+"："+string.Join("；",issues.Select(issue=>issue.Message)));
   }
   var dispatcher=System.Windows.Application.Current?.Dispatcher??Dispatcher.CurrentDispatcher;
   void Post(Action action)
   {
    if(dispatcher.HasShutdownStarted||dispatcher.HasShutdownFinished)throw new InvalidOperationException("所属窗口调度器正在退出，不能投递连续捕获或释放。");
    dispatcher.BeginInvoke(DispatcherPriority.Normal,action);
   }
   var run=DocumentManager.CaptureCurrentContinuousRun(roots,FormalImageInputs,Post);
   _formalContinuousRun=run;_formalReleaseError=null;_continuousCompletionText=null;_continuousDisplayedRound=_continuousCompletedRound=0;_continuousRootRounds.Clear();
   _formalRunningRootIds.UnionWith(roots);
   foreach(var tab in WorkflowTabViewModel.Tabs.Where(tab=>_formalRunningRootIds.Contains(tab.RootWorkflowId))){tab.RunMode=Features.WorkflowUI.RunMode.Continuous;tab.IsRunning=true;}
   NotifyFormalRunState();
   var task=RunContinuousCoreAsync(session,run);_formalRunTask=task;_ = ObserveContinuousCompletionAsync(task);
   return true;
  }
  catch(Exception failure){StatusText="连续运行未启动："+failure.Message;AddLog(StatusText);return false;}
 }
 private async Task ObserveContinuousCompletionAsync(Task task)
 {
  try{await task;}
  catch(Exception error){_continuousCompletionText="连续运行失败："+error.Message;AddLog(_continuousCompletionText);}
  finally{if(ReferenceEquals(_formalRunTask,task))_formalRunTask=null;NotifyFormalRunState();if(_continuousCompletionText is {} completion)StatusText=completion;}
 }
 private async Task RunContinuousCoreAsync(DocumentSession session,DocumentContinuousRun run)
 {
  var rootRun=run.State.RootRunId;
  bool Current()=>!_documentHostReleased&&ReferenceEquals(DocumentManager.CurrentSession,session)&&ReferenceEquals(_formalContinuousRun,run)&&run.State.RootRunId==rootRun;
  void RoundStarted(long round)
  {
   if(round<=_continuousDisplayedRound)return;
   _continuousDisplayedRound=round;
   foreach(var id in _formalRunningRootIds)EnsureFormalExecutionSnapshot(id)?.PrepareContinuousRound(session.GetWorkflows().Single(flow=>flow.Id==id).Name);
  }
  void RootStarted(string id,long round)
  {
   RoundStarted(round);
   if(_continuousRootRounds.GetValueOrDefault(id)==round)return;
   _continuousRootRounds[id]=round;
   EnsureFormalExecutionSnapshot(id)?.BeginContinuous(session.GetWorkflows().Single(flow=>flow.Id==id).Name);
  }
  void State(ContinuousRunState state)
  {
   if(!Current()||state.EditSessionId!=session.EditSessionId||state.RootRunId!=rootRun||state.Round<_continuousDisplayedRound)return;
   if(state.Phase==ContinuousPhase.Running)
   {
    RoundStarted(state.Round);
    if(state.ActiveWorkflowId is {} id&&_formalRunningRootIds.Contains(id))RootStarted(id,state.Round);
   }
   NotifyFormalRunState();
  }
  void Node(ContinuousNodeStatus status)
  {
   if(!Current()||status.RootRunId!=rootRun||status.Round<_continuousDisplayedRound||status.Round<=_continuousCompletedRound||!_formalRunningRootIds.Contains(status.WorkflowId))return;
   var actual=session.FindNode(status.WorkflowId,status.NodeId);if(actual is null)return;
   RootStarted(status.WorkflowId,status.Round);session.DrainRuntimeChanges();
   GetFormalExecutionSnapshot(status.WorkflowId)?.UpdateNodeStatus(status.NodeId,actual.Name,status.Status.ToString());
   RefreshFormalImageReadReceipts(session);RefreshFormalPublishedResult();
  }
  void Round(ContinuousRoundSummary summary)
  {
   if(!Current()||summary.RootRunId!=rootRun||summary.Round<_continuousDisplayedRound||summary.Round<=_continuousCompletedRound)return;
   RoundStarted(summary.Round);session.DrainRuntimeChanges();
   foreach(var workflow in summary.Workflows)
   {
    if(!_formalRunningRootIds.Contains(workflow.WorkflowId))throw new InvalidOperationException("连续摘要包含未预约根。");
    foreach(var node in workflow.Nodes)
     if(node.Identity is {} identity&&(identity.DocumentSessionId!=session.ResultOwnerId||identity.RunId!=rootRun||identity.NodeId!=node.NodeId))throw new InvalidOperationException("连续节点计时摘要不是当前实际运行。");
    EnsureFormalExecutionSnapshot(workflow.WorkflowId)?.CompleteContinuous(workflow,id=>session.FindNode(workflow.WorkflowId,id)?.Name??id);
    foreach(var error in workflow.Errors)AddLog("流程 "+workflow.WorkflowId+"："+error);
    NotifyResultWorkspaceRootSettled(workflow.WorkflowId);
   }
   _continuousCompletedRound=summary.Round;
   RefreshFormalImageReadReceipts(session);RefreshFormalPublishedResult();RefreshStatusBarExecutionSnapshot();
  }
  run.StateChanged+=State;run.NodeStatusChanged+=Node;run.RoundCompleted+=Round;
  try
  {
   var completion=await run.RunAsync(message=>
   {
    var dispatcher=System.Windows.Application.Current?.Dispatcher??throw new InvalidOperationException("正式窗口调度器不存在。");
    dispatcher.BeginInvoke(new Action(()=>{if(Current())AddLog(message);}));
   });
   if(Current())
   {
    var text=completion.Reason switch {ContinuousStopReason.LastImage=>"连续运行已到末图",ContinuousStopReason.BusinessNg=>"连续运行因NG停止",ContinuousStopReason.Requested=>"连续运行已停止",ContinuousStopReason.Cancelled=>"连续运行已取消",_=>"连续运行失败"};
    _continuousCompletionText=text+"；已结算 "+completion.CompletedRounds+" 轮。"+(completion.Error is null?"":" "+completion.Error.Message);StatusText=_continuousCompletionText;AddLog(StatusText);
    if(run.LastObserverError is {} observer)AddLog("连续呈现观察者异常："+observer.Message);
   }
  }
  finally
  {
   run.StateChanged-=State;run.NodeStatusChanged-=Node;run.RoundCompleted-=Round;
   try
   {
    session.DrainRuntimeChanges();RefreshFormalImageReadReceipts(session);RefreshFormalPublishedResult();
   }
   finally
   {
    try{run.Dispose();}
    catch(Exception error){_formalReleaseError=error;throw;}
    if(ReferenceEquals(_formalContinuousRun,run))
    {
     foreach(var id in _formalRunningRootIds)if(GetFormalExecutionSnapshot(id) is {Status:ExecutionStatus.Running} snapshot)snapshot.FinishWithoutResult(ExecutionStatus.Cancelled);
     foreach(var tab in WorkflowTabViewModel.Tabs.Where(tab=>_formalRunningRootIds.Contains(tab.RootWorkflowId)))tab.IsRunning=false;
     _formalRunningRootIds.Clear();_formalContinuousRun=null;_continuousRootRounds.Clear();
     RefreshRootTabReadiness();RefreshStatusBarExecutionSnapshot();NotifyFormalRunState();
    }
   }
  }
 }
 private void RequestStopFormalExecution()
 {
  var errors=new List<Exception>();
  try{_formalContinuousRun?.Stop();}catch(Exception error){errors.Add(error);}
  try{_formalActiveBatch?.Stop();}catch(Exception error){errors.Add(error);}
  NotifyFormalRunState();
  if(errors.Count!=0)throw new AggregateException("停止请求存在异常，仍须等待排空。",errors);
 }
}
