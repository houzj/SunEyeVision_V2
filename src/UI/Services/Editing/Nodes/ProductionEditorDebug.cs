using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Execution;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
namespace SunEyeVision.UI.Services.Editing;

/// <summary>One real editor debug scope. WPF owns capture/accept/drain; workers own execution only.</summary>
internal sealed class ProductionEditorDebug : IAsyncDisposable
{
 private readonly ProductionEditorContext _context;
 private readonly RuntimeNodeConfigurationControl _control;
 private readonly DocumentDebugSession _scope;
 private readonly Func<IHostInputFactory> _factory;
 private readonly Func<bool> _canRun;
 private readonly Action<string> _log;
 private readonly Action _changed;
 private DocumentDebugInvocation? _invocation;
 private Task? _operation, _retirement;
 private Preview? _preview;
 private DeclaredOutputSnapshot? _snapshot;
 internal ResultHistoryBuffer History { get; } = new();
 private NodeConfigurationEditDraft? _snapshotDraft;
 private long _snapshotExecutionRevision, _observedExecutionRevision;
 private long _requestGeneration;
 private bool _closing, _disposed;
 internal ProductionEditorDebug(ProductionEditorContext context,RuntimeNodeConfigurationControl control,
  Func<IHostInputFactory> factory,Func<bool> canRun,Action<string> log,Action changed)
 {
  _context=context;_control=control;_factory=factory;_canRun=canRun;_log=log;_changed=changed;
  _observedExecutionRevision=context.PreparedDraft.ExecutionRevision;
  _scope=context.Session.OpenDebugSession(context.Identity.WorkflowId,(SunEyeVision.Domain.Workflows.NodeDefinition)context.FormalNode);
 }
 internal bool IsBusy=>_operation is not null||_scope.IsContinuous;
 internal bool IsContinuousRequested=>!_closing&&!_disposed&&_scope.IsContinuousRequested;
 internal bool CanRun=>!_closing&&!_disposed&&!IsBusy&&_context.IsValid&&_canRun();
 internal Guid ScopeId=>_scope.Id;
 internal DeclaredOutputSnapshot? CurrentSnapshot=>!_closing&&!_disposed&&_context.IsValid
  &&ReferenceEquals(_context.PreparedDraft,_snapshotDraft)&&_snapshotDraft?.ExecutionRevision==_snapshotExecutionRevision
  &&!_control.HasPendingExecutionEdits?_snapshot:null;
 internal Task RunAsync()
 {
  _control.Dispatcher.VerifyAccess();
  if(!CanRun||IsBusy)throw new InvalidOperationException("当前参数页不可调试，或上次调用尚未排空。");
  return CaptureAndRunAsync();
 }
 internal async Task StartContinuousAsync()
 {
  _control.Dispatcher.VerifyAccess();
  if(!CanRun||IsBusy)throw new InvalidOperationException("当前参数页不可连续调试。");
  // Retire preview before the application scope admits the first invocation.
  ReleasePreview();
  try
  {
   var operation=_scope.StartContinuousAsync(async()=>
   {
    await CaptureAndRunAsync(continuous:true);
    return _snapshot?.IsSuccess==true;
   },()=>!_closing&&!_disposed&&_context.IsValid,_canRun);
   _changed();
   await operation;
  }
  finally{_changed();}
 }
 private async Task CaptureAndRunAsync(bool continuous=false)
 {
  if(_operation is not null)throw new InvalidOperationException("上一调试尚未排空。");
  var drained=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
  _operation=drained.Task;
  try{await CaptureAndExecuteAsync(continuous);}
  finally
  {
   _operation=null;
   drained.TrySetResult();
   _changed();
  }
 }
 private async Task CaptureAndExecuteAsync(bool continuous)
 {
  var generation=_requestGeneration;
  if(!_control.TryCapture(out var error))throw new InvalidOperationException(error);
  var draft=_context.PreparedDraft;
  var executionRevision=_observedExecutionRevision=draft.ExecutionRevision;
  bool CanDispatch()=>OwnsRequest(draft,executionRevision,generation)&&(!continuous||_scope.IsContinuousRequested);
  if(!CanDispatch())throw new OperationCanceledException("调试准备期间调用已退役或连续执行已停止。");
  ReleasePreview();RetireSnapshot();
  if(!CanDispatch())throw new OperationCanceledException("调试准备期间调用已退役或连续执行已停止。");
  var factory=_factory();
  if(!CanDispatch())throw new OperationCanceledException("调试输入准备期间调用已退役或连续执行已停止。");
  var invocation=_scope.Capture(draft,null,_log,hostFactory:factory);
  _invocation=invocation;
  try
  {
   if(!CanDispatch())throw new OperationCanceledException("调试捕获期间调用已退役或连续执行已停止。");
   _changed();
   if(!CanDispatch())throw new OperationCanceledException("调试执行前调用已退役或连续执行已停止。");
  }
  catch
  {
   _invocation=null;
   invocation.Dispose();
   throw;
  }
  await ExecuteCoreAsync(invocation,draft,executionRevision,generation);
 }
 private bool OwnsRequest(NodeConfigurationEditDraft draft,long executionRevision,long generation)
  =>!_closing&&!_disposed&&_context.IsValid&&generation==_requestGeneration
   &&ReferenceEquals(_context.PreparedDraft,draft)&&draft.ExecutionRevision==executionRevision&&!_control.HasPendingExecutionEdits;
 private async Task ExecuteCoreAsync(DocumentDebugInvocation invocation,NodeConfigurationEditDraft draft,long executionRevision,long generation)
 {
  DeclaredOutputSnapshot? result=null;
  var accepted=false;
  bool Current()=>OwnsRequest(draft,executionRevision,generation);
  bool CanReceive()=>Current()&&(accepted||invocation.IsCurrent);
  try
  {
   result=await Task.Run(invocation.ExecuteAsync);
   _control.Dispatcher.VerifyAccess();
   if(!CanReceive())
    throw new OperationCanceledException("调试期间参数页、草稿或用户输入已变化。");
   if(result.IsSuccess)
   {
    invocation.AcceptParameters();
    executionRevision=draft.ExecutionRevision;
    accepted=true;
   }
   if(!Current())return;
   var published=result;
   History.Append(published.ResultRecords, published.CompletedAt);
   _snapshot=published;result=null;_snapshotDraft=draft;
   _snapshotExecutionRevision=_observedExecutionRevision=draft.ExecutionRevision;
   _changed();
   if(ReferenceEquals(CurrentSnapshot,published))
    _control.PublishDebugCompletion(published,published.ErrorMessage);
  }
  catch(Exception error) when(!CanReceive())
  {
   if(error is OperationCanceledException)throw;
   throw new OperationCanceledException("调试调用已退役。",error);
  }
  catch(Exception error) when(error is not OperationCanceledException)
  {
   _control.PublishDebugCompletion(null,error.Message);
   throw;
  }
  finally
  {
   try{result?.Dispose();}
   finally{_invocation=null;invocation.Dispose();}
  }
 }
 internal NativeRoiInputLease CaptureInputs(IReadOnlyCollection<string> fields)
 {
  _control.Dispatcher.VerifyAccess();
  bool Ready()=>!_closing&&!_disposed&&!IsBusy&&_context.IsValid;
  if(!Ready())throw new InvalidOperationException("参数输入预览尚未就绪或正在调试。");
  ReleasePreview();
  if(!Ready()||_preview is not null)throw new InvalidOperationException("参数输入预览已被替换。");
  // A preview reads the accepted draft. Only explicit execution or confirmation commits pending controls.
  var draft=_context.PreparedDraft;
  var invocation=_scope.Capture(draft,null,_log,previewInputs:fields);
  ExecutionInputPreviewLease? inputs=null;
  try
  {
   inputs=invocation.CaptureInputs();
   if(!Ready()||!ReferenceEquals(_context.PreparedDraft,draft)||!inputs.IsCurrent)
    throw new InvalidOperationException("参数输入预览在读取期间已退役。");
   var preview=new Preview(inputs,invocation);inputs=null;_preview=preview;
   return new(preview.Inputs.Inputs,()=>!_closing&&!_disposed&&ReferenceEquals(_preview,preview)&&_context.IsValid&&preview.Inputs.IsCurrent,
    ()=>{if(ReferenceEquals(_preview,preview))_preview=null;preview.Dispose();});
  }
  catch{try{inputs?.Dispose();}finally{invocation.Dispose();}throw;}
 }
 internal void StopContinuous()
 {
  _control.Dispatcher.VerifyAccess();
  _scope.StopContinuous();
  _changed();
 }
 internal void OnDraftEdited()
 {
  var revision=_context.PreparedDraft.ExecutionRevision;
  var changed=revision!=_observedExecutionRevision;
  _observedExecutionRevision=revision;
  if(changed||_control.HasPendingExecutionEdits)InvalidateDraft();
 }
 internal void InvalidateDraft()
 {
  _control.Dispatcher.VerifyAccess();
  ++_requestGeneration;
  _scope.StopContinuous();
  _invocation?.Cancel();
  ReleasePreview();
  RetireSnapshot();
 }
 private void RetireSnapshot()
 {
  var previous=_snapshot;_snapshot=null;_snapshotDraft=null;
  try{_changed();}finally{previous?.Dispose();}
 }
 private void ReleasePreview(){var previous=_preview;_preview=null;previous?.Dispose();}
 public ValueTask DisposeAsync()
 {
  _control.Dispatcher.VerifyAccess();return new(_retirement??=RetireAsync());
 }
 private async Task RetireAsync()
 {
  if(_disposed)return;
  _closing=true;var errors=new List<Exception>();
  void Release(Action action){try{action();}catch(Exception error){errors.Add(error);}}
  Release(_scope.Cancel);Release(ReleasePreview);Release(RetireSnapshot);Release(()=>_invocation?.Cancel());
  if(_operation is {} operation)try{await operation;}catch(Exception error){Release(()=>_log("关闭参数调试已排空："+error.Message));}
  try{await _scope.ContinuousCompletion;}catch(Exception error){Release(()=>_log("连续调试已排空："+error.Message));}
  History.Clear();
  try{_scope.Dispose();_disposed=true;}catch(Exception error){errors.Add(error);}
  Release(_changed);
  if(errors.Count!=0)throw new AggregateException("参数调试清理未全部完成。",errors);
 }
 private sealed class Preview(ExecutionInputPreviewLease inputs,DocumentDebugInvocation invocation):IDisposable
 {
  internal readonly ExecutionInputPreviewLease Inputs=inputs;private bool _disposed;
  public void Dispose(){if(_disposed)return;_disposed=true;try{Inputs.Dispose();}finally{invocation.Dispose();}}
 }
}
