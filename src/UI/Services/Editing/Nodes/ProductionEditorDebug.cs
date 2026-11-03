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
 private NodeConfigurationEditDraft? _snapshotDraft;
 private object? _snapshotConfiguration;
 private bool _closing, _disposed;
 internal ProductionEditorDebug(ProductionEditorContext context,RuntimeNodeConfigurationControl control,
  Func<IHostInputFactory> factory,Func<bool> canRun,Action<string> log,Action changed)
 {
  _context=context;_control=control;_factory=factory;_canRun=canRun;_log=log;_changed=changed;
  _scope=context.Session.OpenDebugSession(context.Identity.WorkflowId,(SunEyeVision.Domain.Workflows.NodeDefinition)context.FormalNode);
 }
 internal bool IsBusy=>_operation is not null;
 internal bool CanRun=>!_closing&&!_disposed&&!IsBusy&&_context.IsValid&&_canRun();
 internal Guid ScopeId=>_scope.Id;
 internal DeclaredOutputSnapshot? CurrentSnapshot=>!_closing&&!_disposed&&_context.IsValid
  &&ReferenceEquals(_context.PreparedDraft,_snapshotDraft)&&ReferenceEquals(_snapshotDraft?.Configuration,_snapshotConfiguration)
  &&!_control.HasPendingEdits?_snapshot:null;
 internal Task RunAsync()
 {
  _control.Dispatcher.VerifyAccess();
  if(!CanRun)throw new InvalidOperationException("当前参数页不可调试，或上次调用尚未排空。");
  if(!_control.TryCapture(out var error))throw new InvalidOperationException(error);
  ReleasePreview();RetireSnapshot();
  var draft=_context.PreparedDraft;
  var invocation=_scope.Capture(draft,null,_log,hostFactory:_factory());_invocation=invocation;
  _operation=ExecuteCoreAsync(invocation,draft);_changed();return ObserveOperationAsync(_operation);
 }
 private async Task ObserveOperationAsync(Task operation)
 {
  try{await operation;}
  finally{if(ReferenceEquals(_operation,operation))_operation=null;_changed();}
 }
 private async Task ExecuteCoreAsync(DocumentDebugInvocation invocation,NodeConfigurationEditDraft draft)
 {
  DeclaredOutputSnapshot? result=null;
  try
  {
   result=await Task.Run(invocation.ExecuteAsync);
   _control.Dispatcher.VerifyAccess();
   if(_closing||!_context.IsValid||!ReferenceEquals(_context.PreparedDraft,draft)||_control.HasPendingEdits)
    throw new InvalidOperationException("调试期间参数页、草稿或用户输入已变化，结果未回写。");
   if(result.IsSuccess)invocation.AcceptParameters();
   _snapshot=result;result=null;_snapshotDraft=draft;_snapshotConfiguration=draft.Configuration;
   _changed();
   _control.PublishDebugCompletion(_snapshot,_snapshot.ErrorMessage);
  }
  catch(Exception error)
  {
   if(!_closing&&_context.IsValid)_control.PublishDebugCompletion(null,error.Message);
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
  if(_closing||_disposed||IsBusy||!_context.IsValid)throw new InvalidOperationException("参数输入预览尚未就绪或正在调试。");
  ReleasePreview();
  if(!_control.TryCapture(out var error))throw new InvalidOperationException(error);
  var invocation=_scope.Capture(_context.PreparedDraft,null,_log,previewInputs:fields);
  ExecutionInputPreviewLease? inputs=null;
  try
  {
   inputs=invocation.CaptureInputs();
   var preview=new Preview(inputs,invocation);inputs=null;_preview=preview;
   return new(preview.Inputs.Inputs,()=>!_closing&&!_disposed&&ReferenceEquals(_preview,preview)&&_context.IsValid&&preview.Inputs.IsCurrent,
    ()=>{if(ReferenceEquals(_preview,preview))_preview=null;preview.Dispose();});
  }
  catch{try{inputs?.Dispose();}finally{invocation.Dispose();}throw;}
 }
 internal void Stop(){_control.Dispatcher.VerifyAccess();_invocation?.Cancel();ReleasePreview();}
 internal void InvalidateDraft(){Stop();RetireSnapshot();_changed();}
 private void RetireSnapshot()
 {
  var previous=_snapshot;_snapshot=null;_snapshotDraft=null;_snapshotConfiguration=null;
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
