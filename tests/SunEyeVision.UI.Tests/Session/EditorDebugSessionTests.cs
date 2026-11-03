using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Editing.Wpf;
using SunEyeVision.Plugin.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.UI.Services.Editing;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class EditorDebugSessionTests
{
 private static RuntimeToolEditorWindow Open(DocumentUiFixture f,NodeDefinition node)
 {
  f.Main.SelectedNode=node;f.Main.OpenDebugWindowCommand.Execute(node);UiRuntime.Pump();
  return System.Windows.Application.Current.Windows.OfType<RuntimeToolEditorWindow>().Single();
 }
 private static void Run(RuntimeToolEditorWindow window)
 {
  Assert.True(window.ExecuteOnce.CanExecute(null));UiRuntime.Await((Task)UiRuntime.Call(window,"RunOnceAsync")!);
  Assert.True(window.Editor.CurrentDebugSnapshot?.IsSuccess,window.Editor.CurrentDebugSnapshot?.ErrorMessage);
 }
 private static string FileOf(DeclaredOutputSnapshot snapshot)
 {using var reader=snapshot.AcquireReader();return reader.Values!.Get<string>("output.file");}
 [Fact] public void Actual_window_private_cursor_result_and_rebased_draft_stay_separate_from_formal_run()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();var first=f.Image(22);var second=f.Image(44);f.ConfigureImage(node,first,second);f.Fields(node,("local.auto-switch",new ConstantSetting(true)));
  var configuration=node.Configuration;var window=Open(f,node);var scope=window.Editor.DebugScopeId;Assert.NotNull(scope);
  var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));
  var settings=UiRuntime.Visuals<DeclaredSettingsView>(window.Editor).Single();Assert.True(settings.TrySetFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(9L)}}));
  Run(window);var one=window.Editor.CurrentDebugSnapshot!;Assert.Equal(second,FileOf(one));Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));Assert.Same(configuration,node.Configuration);
  Assert.Equal(one.Identity,f.Main.CurrentResultPublication!.Stamp.Invocation);Assert.NotEqual(f.Session.ResultOwnerId,one.Identity.DocumentSessionId);
  Run(window);Assert.Equal(first,FileOf(window.Editor.CurrentDebugSnapshot!));Assert.Equal(scope,window.Editor.DebugScopeId);Assert.Equal(0L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("local.start")).Value);
  Assert.Equal(9L,context.ReadCurrentParameter("sequence.initial")?.Value);Assert.True(context.TryCommitEditor(out var error),error);Assert.Equal(scope,window.Editor.DebugScopeId);
  Run(window);Assert.Equal(scope,window.Editor.DebugScopeId);Assert.True(window.CloseAndDrainAsync().GetAwaiter().GetResult());UiRuntime.Pump();Assert.Null(f.Main.CurrentResultPublication?.Stamp.Invocation);
  f.Run();using var formal=f.Session.AcquirePublishedResult(f.Root.Id,node.Id);Assert.NotNull(formal);Assert.Equal(second,FileOf(formal.Snapshot));
 });
 [Fact] public void Actual_blob_window_previews_real_published_input_then_runs_and_retires_old_preview()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var source=f.Add();var blob=f.Add("BlobAnalysis",350,240);f.ConfigureImage(source,f.Image());f.Connect(source,blob);f.Run();
  var window=Open(f,blob);var context=Assert.IsType<ProductionEditorContext>(ToolEditorCommit.GetDraftContext(window.Editor));var scope=window.Editor.DebugScopeId;
  using var inputs=window.Editor.CaptureDebugInputs(new[]{"input.image"});Assert.True(inputs.IsCurrent);Assert.NotNull(inputs.Inputs.Get<IReadOnlyImage>("input.image"));
  Run(window);Assert.False(inputs.IsCurrent);Assert.Equal(scope,window.Editor.DebugScopeId);Assert.True(window.Editor.CurrentDebugSnapshot!.IsSuccess);
  using var next=window.Editor.CaptureDebugInputs(new[]{"input.image"});Assert.True(next.IsCurrent);
  f.Fields(source,("sequence.initial",new ConstantSetting(99L)));Assert.False(next.IsCurrent);
  window.Close();UiRuntime.Pump();Assert.False(context.IsActive);
 });
 [Fact] public void Closing_active_actual_window_waits_for_real_host_prepare_before_disposing_scope()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);var close=editor.Window.CloseAndDrainAsync();
  Assert.False(close.IsCompleted);Assert.True(editor.Window.IsCurrent);Assert.True(editor.Window.Editor.IsDebugBusy);
  gate.Release.TrySetResult();UiRuntime.Await(close);try{UiRuntime.Await(run);}catch(OperationCanceledException){}catch(InvalidOperationException){}
  Assert.True(close.Result);Assert.False(editor.Window.IsCurrent);Assert.False(editor.Context.IsValid);Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));
 });
 [Fact] public void New_raw_text_during_real_debug_is_preserved_and_late_private_values_are_rejected()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var settings=UiRuntime.Visuals<DeclaredSettingsView>(editor.Window.Editor).Single();var fields=(SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView)settings.Content;
  var text=Assert.IsType<TextBox>(fields.Fields.Single(item=>item.FieldId=="sequence.initial").InputElement);var body=editor.Window.Editor.Content;
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);text.Text="尚未输入完";gate.Release.TrySetResult();
  Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));Assert.Equal("尚未输入完",text.Text);Assert.Same(body,editor.Window.Editor.Content);Assert.Null(editor.Window.Editor.CurrentDebugSnapshot);
  text.Text="12";Assert.True(editor.Context.TryCommitEditor(out var error),error);Assert.Equal(12L,Assert.IsType<ConstantSetting>(node.Configuration.Fields.Read("sequence.initial")).Value);
 });
 [Fact] public void Deleted_actual_node_cancels_debug_and_undo_cannot_accept_the_retired_window_result()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);f.Canvas.Delete(new[]{f.Projection(node)},Array.Empty<SunEyeVision.UI.Features.WorkflowUI.Canvas.CanvasConnectionProjection>());
  Assert.False(editor.Context.IsValid);gate.Release.TrySetResult();Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));
  UiRuntime.Await(editor.Window.CloseAndDrainAsync());f.Session.Undo();Assert.Same(node,f.Session.FindNode(f.Root.Id,node.Id));Assert.False(editor.Context.IsValid);Assert.Null(f.Session.AcquirePublishedResult(f.Root.Id,node.Id));
 });
 [Fact] public void Stop_cancels_one_invocation_and_same_actual_window_can_run_again()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);var scope=editor.Window.Editor.DebugScopeId;
  var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);Assert.True(editor.Window.Stop.CanExecute(null));editor.Window.Stop.Execute(null);gate.Release.TrySetResult();
  var failure=Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));Assert.True(failure is OperationCanceledException or InvalidOperationException,failure.ToString());
  Assert.True(editor.Context.IsValid);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);Run(editor.Window);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
 });
 [Fact] public void Receipt_rebase_during_running_rejects_old_invocation_and_preserves_scope_for_new_actual_draft()=>UiRuntime.Run(()=>
 {
  using var f=new DocumentUiFixture();var node=f.Add();f.ConfigureImage(node,f.Image());using var gate=new GatedFactory();using var editor=new PrivateWindow(f,node,gate);var scope=editor.Window.Editor.DebugScopeId;
  var previous=editor.Context.PreparedDraft;var run=editor.Window.Editor.RunDebugAsync();UiRuntime.Await(gate.Entered.Task);
  Assert.True(editor.Context.TryCommitEditor(out var error),error);Assert.NotSame(previous,editor.Context.PreparedDraft);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
  gate.Release.TrySetResult();var failure=Assert.ThrowsAny<Exception>(()=>UiRuntime.Await(run));Assert.True(failure is OperationCanceledException or InvalidOperationException,failure.ToString());Assert.Null(editor.Window.Editor.CurrentDebugSnapshot);
  Run(editor.Window);Assert.Equal(scope,editor.Window.Editor.DebugScopeId);
 });
 private sealed class PrivateWindow:IDisposable
 {
  internal readonly RuntimeToolEditorWindow Window;internal readonly ProductionEditorContext Context;
  internal PrivateWindow(DocumentUiFixture f,NodeDefinition node,IHostInputFactory factory)
  {
   var control=new RuntimeNodeConfigurationControl();Window=new(control){Owner=f.Host,ShowInTaskbar=false};
   Context=new(f.Session,f.Root.Id,node,()=>ReferenceEquals(SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSession,f.Session),control.BindDraft);
   ToolEditorCommit.AttachDraftContext(control,Context);Context.PrepareView=()=>control.TryCapture(out var error)?null:error;Context.HasPendingViewEdits=()=>control.HasPendingEdits;Context.PreserveViewAfterRebase=control.AdoptRebasedDraft;
   control.ConfigureDebug(Context,()=>factory,()=>true,_=>{},_=>null,()=>{});Window.SetRunAvailability(()=>true);
   Context.Deactivated+=control.Dispose;Context.Deactivated+=()=>{if(Window.IsLoaded)Window.Close();};Window.Closed+=(_,_)=>Context.Discard();
   control.BindDraft(Context.PreparedDraft);Window.Show();UiRuntime.Pump();
  }
  public void Dispose(){UiRuntime.Await(Window.CloseAndDrainAsync());Context.Discard();}
 }
 private sealed class GatedFactory:IHostInputFactory,IDisposable
 {
  private readonly ImageSourceHostInputFactory _actual=new();internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public IHostInputPreparer? Create(HostInputFactoryContext context)=>new Gate(_actual.Create(context)!,this);
  public void Dispose()=>_actual.Dispose();
  private sealed class Gate(IHostInputPreparer actual,GatedFactory owner):IHostInputPreparer
  {
   public string CapabilityId=>actual.CapabilityId;public int CapabilityVersion=>actual.CapabilityVersion;public IReadOnlyList<string> Services=>actual.Services;
   public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)
   {owner.Entered.TrySetResult();await owner.Release.Task;context.Cancellation.ThrowIfCancellationRequested();return await actual.PrepareAsync(context);}
   public void Dispose()=>actual.Dispose();
  }
 }
}
