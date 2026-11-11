using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Infrastructure.ImageSources;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 internal bool IsFormalRootRunning(string rootId)=>_formalRunningRootIds.Contains(rootId);
 private ImageSourceHostInputFactory? _formalImageInputs;
 private bool _folderTrackingQueued;
 private readonly object _folderRunGate = new();
 private readonly Dictionary<(Guid Session, string Workflow, string Node), List<HostInputFactoryContext>> _folderRunsQueued = new();
 private DocumentRunBatch? _formalActiveBatch;
 private Task? _formalRunTask;
 private readonly HashSet<string> _formalRunningRootIds=new(StringComparer.Ordinal);
 private readonly Dictionary<string,WorkflowExecutionSnapshot> _formalExecutionSnapshots=new(StringComparer.Ordinal);
 private ImageSourceHostInputFactory FormalImageInputs=>_formalImageInputs??=CreateFormalImageInputs();

 private ImageSourceHostInputFactory CreateFormalImageInputs()
 {
  var dispatcher = System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
  return new(SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime,
   context => QueueFolderRun(dispatcher, context));
 }
 private void QueueFolderRun(Dispatcher dispatcher, HostInputFactoryContext context)
 {
  var key = (Session: context.EditSessionId, Workflow: context.Node.WorkflowId, Node: context.Node.NodeId);
  if (dispatcher.HasShutdownStarted) return;
  lock (_folderRunGate)
  {
   if (_folderRunsQueued.TryGetValue(key, out var pending))
   {
    if (!pending.Contains(context)) pending.Add(context);
    return;
   }
   _folderRunsQueued.Add(key, new() { context });
  }
  dispatcher.BeginInvoke(new Action(async () =>
  {
   List<HostInputFactoryContext> requests;
   lock (_folderRunGate)
    if (!_folderRunsQueued.Remove(key, out requests!)) return;
   try
   {
    if (_documentHostReleased || !CanAcceptExternalActivation || IsDebugWindowOpen || IsRunning || _formalRunTask is not null ||
        DocumentManager.CurrentSession is not { } session || session.EditSessionId != key.Session) return;
    var accepted = false;
    for (var i = requests.Count - 1; i >= 0 && !accepted; i--)
    {
     try { requests[i].EnsureCurrent(); accepted = true; }
     catch (InvalidOperationException) { }
    }
    if (!accepted || !session.GetWorkflows().Any(flow => !flow.IsSubroutine && flow.Id == key.Workflow)) return;
    await RunFormalWorkflowsAsync(new[] { key.Workflow });
   }
   catch (Exception error) { AddLog("订阅文件夹触发流程失败：" + error.Message); }
  }));
 }
 private void QueueFolderTriggerTracking()
 {
  if (_folderTrackingQueued || _documentHostReleased || DocumentManager.CurrentSession is null) return;
  _folderTrackingQueued = true;
  Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
  {
   _folderTrackingQueued = false;
   if (_documentHostReleased || DocumentManager.CurrentSession is not { } session) return;
   foreach (var flow in session.GetWorkflows().Where(flow => !flow.IsSubroutine))
    foreach (var node in session.GetNodes(flow.Id))
    {
     if (!node.Configuration.Fields.Declarations.ContainsKey("local.trigger-folder-run") ||
         node.Configuration.Fields.Read("local.trigger-folder-run") is not ConstantSetting { Value: true }) continue;
     try
     {
      if (session.CaptureHostInputContext(flow.Id, node) is { } context)
       FormalImageInputs.TrackFolderTrigger(context);
     }
     catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.IO.IOException or UnauthorizedAccessException)
     { AddLog("订阅文件夹监听失败：" + error.Message); }
    }
  }));
 }
 private WorkflowExecutionSnapshot? GetFormalExecutionSnapshot(string workflowId)=>_formalExecutionSnapshots.GetValueOrDefault(workflowId);
 private WorkflowExecutionSnapshot? EnsureFormalExecutionSnapshot(string workflowId)
 {
  var session=DocumentManager.CurrentSession;
  var root=session?.GetWorkflows().SingleOrDefault(flow=>flow.Id==workflowId&&!flow.IsSubroutine);
  if(root is null)return null;
  if(!_formalExecutionSnapshots.TryGetValue(workflowId,out var snapshot))_formalExecutionSnapshots.Add(workflowId,snapshot=new(workflowId,root.Name));
  snapshot.SynchronizeDefinition(root.Name,root.Nodes);return snapshot;
 }
 private async Task RunFormalWorkflowsAsync(IReadOnlyList<string> workflowIds, int navigationDirection = 0)
 {
  if(_formalRunTask is not null||IsRunning)throw new InvalidOperationException("方案已有正式运行，请先停止并等待排空。");
  var session=DocumentManager.CurrentSession??throw new InvalidOperationException("当前没有方案。");
  if(!CanEditRootWorkspace||IsDebugWindowOpen)throw new InvalidOperationException("请先处理参数编辑和方案切换后再运行。");
  if(!TryCommitSolutionEditors(out var error))throw new InvalidOperationException(error);
  _formalRunTask=RunFormalBatchCoreAsync(session,workflowIds,navigationDirection);
  try{await _formalRunTask;}finally{_formalRunTask=null;}
 }
 private async Task RunFormalBatchCoreAsync(DocumentSession session,IReadOnlyList<string> workflowIds,int navigationDirection)
 {
  if(workflowIds.Count==0)throw new InvalidOperationException("没有可运行的根流程。");
  var dispatcher=System.Windows.Application.Current?.Dispatcher??Dispatcher.CurrentDispatcher;
  var inputFactory=navigationDirection==0?FormalImageInputs:FormalImageInputs.ForNavigation(workflowIds.Single(),navigationDirection);
  using var batch=DocumentManager.CaptureCurrentRunBatch(workflowIds,hostFactory:inputFactory);
  if(!ReferenceEquals(DocumentManager.CurrentSession,session))throw new InvalidOperationException("运行捕获期间方案已变化。");
  _formalActiveBatch=batch;
  void Status(string workflowId,string nodeId,ExecutionStatus status)=>dispatcher.BeginInvoke(new Action(()=>
  {
   if(!ReferenceEquals(_formalActiveBatch,batch)||!ReferenceEquals(DocumentManager.CurrentSession,session))return;
   try
   {
    session.DrainRuntimeChanges();var node=session.FindNode(workflowId,nodeId);
    if(node is not null)GetFormalExecutionSnapshot(workflowId)?.UpdateNodeStatus(nodeId,node.Name,status.ToString());
   }
   catch(Exception error){AddLog("运行状态呈现失败："+error.Message);}
  }));
  batch.NodeStatusChanged+=Status;
  try
  {
  foreach(var id in workflowIds)EnsureFormalExecutionSnapshot(id)?.Begin(session.GetWorkflows().Single(flow=>flow.Id==id).Name);
  _formalRunningRootIds.UnionWith(workflowIds);
  foreach(var tab in WorkflowTabViewModel.Tabs.Where(tab=>_formalRunningRootIds.Contains(tab.RootWorkflowId))){tab.RunMode=SunEyeVision.UI.Features.WorkflowUI.RunMode.Single;tab.IsRunning=true;}
  NotifyFormalRunState();
   using var result=await Task.Run(()=>batch.RunAsync(message=>dispatcher.BeginInvoke(new Action(()=>AddLog(message)))));
   session.DrainRuntimeChanges();
   if(!ReferenceEquals(DocumentManager.CurrentSession,session))return;
   foreach(var item in result.Workflows)
   {
    GetFormalExecutionSnapshot(item.Key)?.Complete(item.Value,id=>session.FindNode(item.Key,id)?.Name??id);
    _formalRunningRootIds.Remove(item.Key);
    var tab=WorkflowTabViewModel.Tabs.SingleOrDefault(tab=>tab.RootWorkflowId==item.Key);if(tab is not null)tab.IsRunning=false;
    AddLog("流程 "+session.GetWorkflows().Single(flow=>flow.Id==item.Key).Name+"："+item.Value.Status);
    NotifyResultWorkspaceRootSettled(item.Key);
   }
   RefreshFormalImageReadReceipts(session);RefreshFormalPublishedResult();RefreshStatusBarExecutionSnapshot();
  }
  finally
  {
   batch.NodeStatusChanged-=Status;
   try{session.DrainRuntimeChanges();batch.Dispose();}
   finally
   {
    foreach(var id in _formalRunningRootIds)GetFormalExecutionSnapshot(id)?.FinishWithoutResult(ExecutionStatus.Cancelled);
    foreach(var tab in WorkflowTabViewModel.Tabs.Where(tab=>_formalRunningRootIds.Contains(tab.RootWorkflowId)))tab.IsRunning=false;
    _formalRunningRootIds.Clear();_formalActiveBatch=null;_singlePaused=false;RefreshRootTabReadiness();NotifyFormalRunState();
   }
  }
 }
 private async Task ActivateFormalImageSourceAsync(ImageSourceActivation activation)
 {
  if(!ReferenceEquals(DocumentManager.CurrentSession,activation.Session)||!CanAcceptExternalActivation||IsDebugWindowOpen)
   throw new InvalidOperationException("图像导航所属方案或编辑状态已变化。");
  var context=activation.Session.CaptureHostInputContext(activation.Workflow.Id,activation.Node)
   ??throw new InvalidOperationException("图像源宿主能力已失效。");
  var binding=ActiveImageSourceBinding;
  if(binding is null||!binding.OwnsNavigation(activation))throw new InvalidOperationException("来源条或导航选择已替换。");
  FormalImageInputs.SelectIndex(context,activation.Index);
  if(!binding.TryPresentNavigation(activation))throw new InvalidOperationException("导航接纳期间来源条已变化。");
  ResultWorkspaceChanged?.Invoke();
  if(IsRunning){StatusText="图像导航已排队，当前已捕获输入保持冻结。";return;}
  await RunFormalWorkflowsAsync(new[]{activation.Workflow.Id});
 }
 private void RefreshFormalImageReadReceipts(DocumentSession session)
 {
  var binding=ActiveImageSourceBinding;var tab=WorkflowTabViewModel.SelectedTab;
  if(binding is null||tab is null||!ReferenceEquals(DocumentManager.CurrentSession,session))return;
  var root=session.GetWorkflows().SingleOrDefault(flow=>flow.Id==tab.RootWorkflowId&&!flow.IsSubroutine);
  var node=root?.Nodes.SingleOrDefault(item=>item.Id==binding.Source.NodeId);
  if(root is null||node is null||!binding.Owns(session,root,node))return;
  var context=session.CaptureHostInputContext(root.Id,node);if(context is null)return;
  foreach(var receipt in FormalImageInputs.TakeCompletedReads(context))
  {
   if(_formalContinuousRun is {} continuous)
   {
    using var published=session.AcquirePublishedResult(root.Id,node.Id);
    if(published is null||published.Snapshot.Identity.RunId!=continuous.State.RootRunId||published.Snapshot.Identity.RequestId!=receipt.Read.RequestId)continue;
   }
   if(!FormalImageInputs.IsReceiptCurrent(context,receipt)||!ReferenceEquals(binding,ActiveImageSourceBinding))continue;
   binding.TryPresentCompletedRead(FormalImageInputs,receipt,binding.Source.EditVersion);
  }
 }
 private async Task StopFormalExecutionAsync(TimeSpan timeout)
 {
  Exception? stopError=null;try{RequestStopFormalExecution();}catch(Exception error){stopError=error;}var task=_formalRunTask;
  if(task is null)return;
  await task.WaitAsync(timeout);
  if(_formalReleaseError is {} release)throw new InvalidOperationException("正式执行释放失败，方案保持打开。",release);
  if(stopError is not null)throw stopError;
 }
 private void ReleaseFormalExecution()
 {
  if(_formalRunTask is {IsCompleted:false}||IsRunning)throw new InvalidOperationException("正式运行尚未排空，不能释放输入工厂。");
  lock (_folderRunGate) _folderRunsQueued.Clear();
  _formalImageInputs?.Dispose();_formalImageInputs=null;_formalExecutionSnapshots.Clear();
 }
}
