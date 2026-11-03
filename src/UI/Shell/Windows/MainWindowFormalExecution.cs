using SunEyeVision.Plugin.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using SunEyeVision.Application;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.ImageSources;
using SunEyeVision.UI.Services.Images;
using SunEyeVision.UI.Services.Workflow;
namespace SunEyeVision.UI.Shell.Windows;
public partial class MainWindowViewModel
{
 internal bool IsFormalRootRunning(string rootId)=>_formalRunningRootIds.Contains(rootId);
 private ImageSourceHostInputFactory? _formalImageInputs;
 private DocumentRunBatch? _formalActiveBatch;
 private Task? _formalRunTask;
 private readonly HashSet<string> _formalRunningRootIds=new(StringComparer.Ordinal);
 private readonly Dictionary<string,WorkflowExecutionSnapshot> _formalExecutionSnapshots=new(StringComparer.Ordinal);
 private ImageSourceHostInputFactory FormalImageInputs=>_formalImageInputs??=new ImageSourceHostInputFactory(
  SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.CameraRuntime.Runtime);
 private WorkflowExecutionSnapshot? GetFormalExecutionSnapshot(string workflowId)=>_formalExecutionSnapshots.GetValueOrDefault(workflowId);
 private WorkflowExecutionSnapshot? EnsureFormalExecutionSnapshot(string workflowId)
 {
  var session=DocumentManager.CurrentSession;
  var root=session?.GetWorkflows().SingleOrDefault(flow=>flow.Id==workflowId&&!flow.IsSubroutine);
  if(root is null)return null;
  if(!_formalExecutionSnapshots.TryGetValue(workflowId,out var snapshot))_formalExecutionSnapshots.Add(workflowId,snapshot=new(workflowId,root.Name));
  snapshot.SynchronizeDefinition(root.Name,root.Nodes);return snapshot;
 }
 private async Task RunFormalWorkflowsAsync(IReadOnlyList<string> workflowIds)
 {
  if(_formalRunTask is not null||IsRunning)throw new InvalidOperationException("方案已有正式运行，请先停止并等待排空。");
  var session=DocumentManager.CurrentSession??throw new InvalidOperationException("当前没有方案。");
  if(!CanEditRootWorkspace||IsDebugWindowOpen)throw new InvalidOperationException("请先处理参数编辑和方案切换后再运行。");
  if(!TryCommitSolutionEditors(out var error))throw new InvalidOperationException(error);
  _formalRunTask=RunFormalBatchCoreAsync(session,workflowIds);
  try{await _formalRunTask;}finally{_formalRunTask=null;}
 }
 private async Task RunFormalBatchCoreAsync(DocumentSession session,IReadOnlyList<string> workflowIds)
 {
  if(workflowIds.Count==0)throw new InvalidOperationException("没有可运行的根流程。");
  var dispatcher=System.Windows.Application.Current?.Dispatcher??Dispatcher.CurrentDispatcher;
  using var batch=DocumentManager.CaptureCurrentRunBatch(workflowIds,hostFactory:FormalImageInputs);
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
    _formalRunningRootIds.Clear();_formalActiveBatch=null;NotifyFormalRunState();
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
  if(binding is null||!binding.Owns(activation.Session,activation.Workflow,activation.Node))throw new InvalidOperationException("来源条已替换。");
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
   if(!FormalImageInputs.IsReceiptCurrent(context,receipt)||!ReferenceEquals(binding,ActiveImageSourceBinding))continue;
   binding.TryPresentCompletedRead(FormalImageInputs,receipt,binding.Source.EditVersion);
  }
 }
 private async Task StopFormalExecutionAsync(TimeSpan timeout)
 {
  _formalActiveBatch?.Stop();var task=_formalRunTask;
  if(task is null)return;
  await task.WaitAsync(timeout);
 }
 private void ReleaseFormalExecution()
 {
  if(_formalRunTask is {IsCompleted:false})throw new InvalidOperationException("正式运行尚未排空，不能释放输入工厂。");
  _formalImageInputs?.Dispose();_formalImageInputs=null;_formalExecutionSnapshots.Clear();
 }
}
