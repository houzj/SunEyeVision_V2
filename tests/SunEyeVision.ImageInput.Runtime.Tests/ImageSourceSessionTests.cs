using System.IO;
using NSubstitute;
using SunEyeVision.Application;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.HalAdapter;
using SunEyeVision.Infrastructure.Persistence;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Infrastructure.ImageSources;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using Xunit;
namespace SunEyeVision.ImageInput.Runtime.Tests;
[Collection("正式WPF owner")]
public sealed class ImageSourceSessionTests
{
 [Fact] public void Cancelled_read_identity_rejects_before_admission_with_cancellation()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();using var cancellation=new CancellationTokenSource();var owner=new ToolExecutionOwner();var identity=owner.Begin("source",Guid.NewGuid(),"cancelled",cancellation.Token);bool admitted=false;
  using var input=new ImageSourceHostInputPreparer("unused",7){LocalReadAdmission=(_,_)=>{admitted=true;return "unused";}};var draft=f.Draft();
  var values=new InputValues(draft.SettingsSchema.Inputs,draft.SettingsSchema.Inputs.Where(port=>draft.Configuration.Fields.Read(port.Id) is ConstantSetting).ToDictionary(port=>port.Id,port=>((ConstantSetting)draft.Configuration.Fields.Read(port.Id)).Value));
  var context=new HostInputPreparationContext(f.Session.GetNodeHostInputDeclaration(f.Flow.Id,f.Node)!,values,identity.ToInvocationIdentity(f.Node.Id),cancellation.Token,new Gate(identity));
  cancellation.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>RuntimeHost.Wait(input.PrepareAsync(context).AsTask()));Assert.False(admitted);identity.CompleteCallbacks();owner.Retire();
 });
 [Fact] public void Local_debug_missing_frame_is_consumed_and_next_request_reads_the_following_frame()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var good=f.Write("good.pgm",20);f.Paths(good,Path.Combine(f.DirectoryPath,"missing.pgm"));f.Edit(("local.auto-switch",new ConstantSetting(true)));var scope=f.Scope();
  using(var fail=f.Capture(scope)){Assert.ThrowsAny<Exception>(()=>{using var result=f.Execute(fail);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});}
  using var next=f.Capture(scope);using var result=f.Execute(next);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();Assert.Equal(good,reader.Values!.Get<string>("output.file"));Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Debug_owners_keep_independent_cursors_and_a_reopened_editor_starts_fresh()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var paths=new[]{f.Write("a.pgm",11),f.Write("b.pgm",22),f.Write("c.pgm",33)};f.Paths(paths);f.Edit(("local.auto-switch",new ConstantSetting(true)));var first=f.Scope();var second=f.Scope();
  string Run(DocumentDebugSession scope){using var call=f.Capture(scope);using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();return reader.Values!.Get<string>("output.file");}
  Assert.Equal(paths[1],Run(first));Assert.Equal(paths[1],Run(second));Assert.Equal(paths[2],Run(first));first.Dispose();Assert.Equal(paths[1],Run(f.Scope()));Assert.Equal(paths[2],Run(second));
 });
 [Theory][InlineData(false)][InlineData(true)] public void Invalid_debug_draft_preserves_previous_cursor_when_the_draft_is_restored(bool empty)=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var path=f.Write("a.pgm",20);f.Paths(path);var scope=f.Scope();using(var call=f.Capture(scope))using(var result=f.Execute(call))Assert.True(result.IsSuccess,result.ErrorMessage);
  var draft=f.Draft();draft.ApplyFields(empty?new Dictionary<string,ParameterSetting>{{"local.paths",new ConstantSetting(Array.Empty<string>())},{"local.start",new ConstantSetting(-1L)}}:new Dictionary<string,ParameterSetting>{{"local.run-mode",new ConstantSetting("selected")},{"local.selected",new ConstantSetting(Array.Empty<string>())}});
  Assert.ThrowsAny<Exception>(()=>{using var invalid=f.Capture(scope,draft);using var result=f.Execute(invalid);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});
  using var restored=f.Capture(scope);using var good=f.Execute(restored);Assert.True(good.IsSuccess,good.ErrorMessage);using var reader=good.AcquireReader();Assert.Equal(path,reader.Values!.Get<string>("output.file"));
 });
 [Fact] public void Local_debug_freezes_private_settings_base_and_sequence_without_publishing_formal_state()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var path=f.Write("relative.pgm",20);f.Paths("relative.pgm");f.Session.SaveAs(Path.Combine(f.DirectoryPath,"saved.solution"),Path.Combine(f.DirectoryPath,"recovery"),f.DirectoryPath,new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string,string>>()),new System.Text.Json.JsonSerializerOptions(),scope=>new SdkVariableValueExtensionCodec(scope));
  var scope=f.Scope();var draft=f.Draft();using var call=f.Capture(scope,draft);using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();Assert.Equal(path,reader.Values!.Get<string>("output.file"));Assert.Equal(7L,reader.Values.Get<long>("output.sequence"));Assert.Equal(new byte[]{20,20},RuntimeFixture.Image(reader).CopyPixels());
  draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"sequence.initial",new ConstantSetting(88L)}});Assert.Throws<InvalidOperationException>(call.AcceptParameters);Assert.Equal(7L,Assert.IsType<ConstantSetting>(f.Node.Configuration.Fields.Read("sequence.initial")).Value);Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Theory][InlineData("empty")][InlineData("excluded")][InlineData("base")] public void Invalid_local_debug_never_uses_a_default_or_upstream_image(string failure)=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();if(failure=="empty")f.Paths();if(failure=="excluded"){f.Paths(f.Write("a.pgm",40));f.Edit(("local.run-mode",new ConstantSetting("selected")),("local.selected",new ConstantSetting(Array.Empty<string>())));}if(failure=="base")f.Paths("relative.pgm");
  Assert.ThrowsAny<Exception>(()=>{using var call=f.Capture(f.Scope());using var result=f.Execute(call);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Formal_request_releases_input_when_run_cannot_start()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",10));var graph=f.Session.PrepareGraphEdit(f.Flow.Id);var second=f.Session.CreateNode(graph,"ImageLoad",new(350,120));f.Session.CommitGraphEdit(graph,"第二个真实来源");var ready=f.Session.Editing.Host.BeginEdit(f.Flow.Id,second.Id);ready.ApplyFields(new Dictionary<string,ParameterSetting>{{"local.paths",f.Node.Configuration.Fields.Read("local.paths")},{"local.start",new ConstantSetting(0L)}});f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{ready}));var first=new CountedPreparer();var count=0;
  var factory=new DelegateFactory(_=>++count==1?first:throw new InvalidOperationException("第二来源捕获失败"));Assert.Throws<InvalidOperationException>(()=>f.Session.CaptureRunBatch(new[]{f.Flow.Id},hostFactory:factory));Assert.Equal(1,first.Disposals);Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Formal_request_reentry_is_rejected_without_disrupting_the_first_run()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",33));using var gate=new BlockingFactory(f.Factory);using var batch=f.Session.CaptureRunBatch(new[]{f.Flow.Id},hostFactory:gate);var running=Task.Run(()=>batch.RunAsync(_=>{}));RuntimeHost.Wait(gate.Entered.Task);
  try{Assert.Throws<InvalidOperationException>(()=>RuntimeHost.Wait(batch.RunAsync(_=>{})));Assert.Throws<InvalidOperationException>(batch.Dispose);}finally{gate.Release.TrySetResult();}
  using var result=RuntimeHost.Wait(running);f.Session.DrainRuntimeChanges();Assert.True(result.Workflows[f.Flow.Id].Success);batch.Dispose();
 });
 [Fact] public void Direct_formal_source_inputs_and_debug_share_pixels_domain_but_keep_distinct_run_and_frame_identities()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",40));using var formal=f.Formal();using var call=f.Capture(f.Scope());using var result=f.Execute(call);using var debug=result.AcquireReader();Assert.True(result.IsSuccess,result.ErrorMessage);
  var a=RuntimeFixture.Image(formal);var b=RuntimeFixture.Image(debug);Assert.Equal(a.CopyPixels(),b.CopyPixels());Assert.NotEqual(a.Frame,b.Frame);Assert.NotEqual(a.Frame.ProducerRun,b.Frame.ProducerRun);Assert.Equal(result.Identity,b.Frame.ProducerRun);using var da=a.EffectiveDomain.AcquireReader();using var db=b.EffectiveDomain.AcquireReader();Assert.Equal(da.ReadRuns(0,4096).ToArray(),db.ReadRuns(0,4096).ToArray());
 });
 [Fact] public void Direct_formal_execution_rejects_missing_scheduler_input_and_persistent_constant_bypass()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();var path=f.Write("a.pgm",50);f.Paths(path);using(var batch=f.Session.CaptureRunBatch(new[]{f.Flow.Id})){using var result=RuntimeHost.Wait(Task.Run(()=>batch.RunAsync(_=>{})));Assert.False(result.Workflows[f.Flow.Id].Success);f.Session.DrainRuntimeChanges();}
  var draft=f.Draft();draft.ApplyFields(new Dictionary<string,ParameterSetting>{{"input.local-address",new ConstantSetting(path)},{"input.sequence",new ConstantSetting(7L)}});
  try{f.Session.CommitEdit(SunEyeVision.Application.Editing.DocumentEditProposal.Prepare(f.Session.Editing.Host,nodes:new[]{draft}));}catch(ArgumentException){}
  using(var bypass=f.Session.CaptureRunBatch(new[]{f.Flow.Id})){using var rejected=RuntimeHost.Wait(Task.Run(()=>bypass.RunAsync(_=>{})));Assert.False(rejected.Workflows[f.Flow.Id].Success);f.Session.DrainRuntimeChanges();}
  f.Edit(("input.local-address",new UnsetSetting()),("input.sequence",new UnsetSetting()));using var good=f.Formal();Assert.True(good.Snapshot.IsSuccess);Assert.Equal(new byte[]{50,50},RuntimeFixture.Image(good).CopyPixels());
 });
 [Fact] public void Camera_debug_uses_frozen_device_timeout_metadata_and_releases_acquired_frame_before_returning()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();var service=Substitute.For<ICameraCaptureService>();CameraCaptureRequest? captured=null;service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(call=>{captured=call.Arg<CameraCaptureRequest>();return Task.FromResult(frame.Frame);});using var f=new RuntimeFixture(service);f.Camera();using var call=f.Capture(f.Scope());using var result=f.Execute(call);Assert.True(result.IsSuccess,result.ErrorMessage);using var reader=result.AcquireReader();
  Assert.Equal("camera",captured!.CameraRef.Value);Assert.Equal(TimeSpan.FromMilliseconds(1234),captured.Timeout);Assert.Equal(CameraTriggerMode.Continuous,captured.TriggerMode);Assert.Equal(result.Identity.RequestId.ToString("N"),captured.DiagnosticCorrelationId);Assert.True(frame.Frame.IsDisposed);Assert.Equal(1,frame.ImageDisposals);Assert.Equal("42",reader.Values!.Get<string>("output.device-frame-id"));Assert.Equal(new byte[]{30,80},RuntimeFixture.Image(reader).CopyPixels());
 });
 [Theory][InlineData("wrong-device")][InlineData("untyped")][InlineData("missing-device")][InlineData("service-failure")] public void Camera_debug_rejects_invalid_capture_without_success_publication_or_resource_leaks(string failure)=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame(failure=="wrong-device"?"other":"camera",failure!="untyped");var service=Substitute.For<ICameraCaptureService>();int captures=0;service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(_=>{captures++;return failure=="service-failure"?Task.FromException<CameraFrame>(new IOException("采集失败")):Task.FromResult(frame.Frame);});using var f=new RuntimeFixture(service);f.Camera(failure=="missing-device"?"":"camera");
  Assert.ThrowsAny<Exception>(()=>{using var call=f.Capture(f.Scope());using var result=f.Execute(call);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});Assert.Equal(failure=="missing-device"?0:1,captures);Assert.Equal(failure is "wrong-device" or "untyped"?1:0,frame.ImageDisposals);Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Camera_cancel_before_debug_capture_does_not_admit_or_call_the_service()=>RuntimeHost.Run(()=>
 {
  var service=Substitute.For<ICameraCaptureService>();using var f=new RuntimeFixture(service);f.Camera();using var cancellation=new CancellationTokenSource();cancellation.Cancel();Assert.ThrowsAny<Exception>(()=>{using var call=f.Capture(f.Scope(),cancellation:cancellation.Token);using var result=f.Execute(call);if(!result.IsSuccess)throw new InvalidOperationException(result.ErrorMessage);});Assert.Empty(service.ReceivedCalls());Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Theory][InlineData(false)][InlineData(true)] public void Camera_debug_retains_original_failure_and_frame_cleanup_failure(bool cancelled)=>RuntimeHost.Run(()=>
 {
  var image=Substitute.For<IVisionImage>();image.When(x=>x.Dispose()).Do(_=>throw new InvalidOperationException("frame-release-failed"));var frame=new CameraFrame(new(cancelled?"camera":"other"),42,DateTimeOffset.UtcNow,image);using var cancellation=new CancellationTokenSource();var service=Substitute.For<ICameraCaptureService>();service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(_=>{if(cancelled)cancellation.Cancel();return Task.FromResult(frame);});using var f=new RuntimeFixture(service);f.Camera();using var call=f.Capture(f.Scope(),cancellation:cancellation.Token);
  var error=Assert.ThrowsAny<Exception>(()=>f.Execute(call));IEnumerable<Exception> errors=error is AggregateException aggregate?aggregate.Flatten().InnerExceptions:new[]{error}.ToList();Assert.Contains(errors,e=>e.Message=="frame-release-failed");Assert.Contains(errors,e=>cancelled?e is OperationCanceledException:e.Message.Contains("设备身份不匹配"));Assert.True(frame.IsDisposed);image.Received(1).Dispose();
 });
 [Fact] public void Direct_formal_borrowed_camera_frame_remains_host_owned_after_run_disposal()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();var service=Substitute.For<ICameraCaptureService>();using var f=new RuntimeFixture(service);f.Camera();var factory=new DelegateFactory(_=>new ImageSourceHostInputPreparer(null,7,service,frame.Frame));using(var reader=f.Formal(factory)){Assert.True(reader.Snapshot.IsSuccess,reader.Snapshot.ErrorMessage);Assert.Equal(new byte[]{30,80},RuntimeFixture.Image(reader).CopyPixels());Assert.False(frame.Frame.IsDisposed);}Assert.False(frame.Frame.IsDisposed);Assert.Empty(service.ReceivedCalls());frame.Dispose();Assert.Equal(1,frame.ImageDisposals);
 });
 [Fact] public void Camera_debug_cancellation_disposes_late_frame_and_inflight_close_waits_for_drain()=>RuntimeHost.Run(()=>
 {
  using var frame=new TrackedFrame();var service=Substitute.For<ICameraCaptureService>();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var late=new TaskCompletionSource<CameraFrame>(TaskCreationOptions.RunContinuationsAsynchronously);service.CaptureAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<CancellationToken>()).Returns(_=>{entered.TrySetResult();return late.Task;});using var f=new RuntimeFixture(service);f.Camera();var scope=f.Scope();using var call=f.Capture(scope);var task=Task.Run(call.ExecuteAsync);RuntimeHost.Wait(entered.Task);
  try{Assert.Throws<InvalidOperationException>(call.Dispose);Assert.Throws<InvalidOperationException>(scope.Dispose);Assert.ThrowsAny<Exception>(()=>RuntimeHost.Wait(call.ExecuteAsync()));call.Cancel();}finally{late.TrySetResult(frame.Frame);}
  Assert.ThrowsAny<OperationCanceledException>(()=>RuntimeHost.Wait(task));Assert.True(frame.Frame.IsDisposed);Assert.Equal(1,frame.ImageDisposals);call.Dispose();scope.Dispose();Assert.Null(f.Session.AcquirePublishedResult(f.Flow.Id,f.Node.Id));
 });
 [Fact] public void Debug_snapshot_is_single_use_and_retained_output_readers_survive_result_close()=>RuntimeHost.Run(()=>
 {
  using var f=new RuntimeFixture();f.Paths(f.Write("a.pgm",60));var scope=f.Scope();var abandoned=f.Capture(scope);abandoned.Dispose();abandoned.Dispose();Assert.ThrowsAny<Exception>(()=>f.Execute(abandoned));using var call=f.Capture(scope);using var result=f.Execute(call);var reader=result.AcquireReader();var image=RuntimeFixture.Image(reader);using var pixels=image.AcquirePixels();using var domain=pixels.AcquireDomainReader();var expected=domain.ReadRuns(0,4096).ToArray();reader.Dispose();result.Dispose();Assert.Throws<ObjectDisposedException>(()=>image.Frame);Assert.Equal(new byte[]{60,60},pixels.GetRow(0).ToArray());pixels.Dispose();Assert.Throws<ObjectDisposedException>(()=>pixels.GetRow(0).ToArray());Assert.Equal(expected,domain.ReadRuns(0,4096).ToArray());domain.Dispose();Assert.Throws<ObjectDisposedException>(()=>domain.SourceFrame);Assert.ThrowsAny<Exception>(()=>f.Execute(call));call.Dispose();
 });
 private sealed class Gate(ToolExecutionIdentity identity):IHostInvocationGate{public bool TryInvoke(Action callback)=>identity.TryInvokeCallback(callback);}
 private sealed class DelegateFactory(Func<HostInputFactoryContext,IHostInputPreparer> create):IHostInputFactory{public IHostInputPreparer Create(HostInputFactoryContext context)=>create(context);}
 private sealed class CountedPreparer:IHostInputPreparer{public int Disposals;public string CapabilityId=>"image-source";public int CapabilityVersion=>1;public IReadOnlyList<string> Services=>new[]{"local-image-read"};public ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context)=>throw new InvalidOperationException();public void Dispose()=>Disposals++;}
 private sealed class BlockingFactory(IHostInputFactory actual):IHostInputFactory,IDisposable
 {
  internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
  public IHostInputPreparer? Create(HostInputFactoryContext context)=>new Block(actual.Create(context)!,this);public void Dispose()=>Release.TrySetResult();
  private sealed class Block(IHostInputPreparer actual,BlockingFactory owner):IHostInputPreparer{public string CapabilityId=>actual.CapabilityId;public int CapabilityVersion=>actual.CapabilityVersion;public IReadOnlyList<string> Services=>actual.Services;public async ValueTask<IPreparedHostInputs> PrepareAsync(HostInputPreparationContext context){owner.Entered.TrySetResult();await owner.Release.Task;return await actual.PrepareAsync(context);}public void Dispose()=>actual.Dispose();}
 }
 private sealed class TrackedFrame:IDisposable
 {
  private readonly HalImage? _native;public int ImageDisposals{get;private set;}public CameraFrame Frame{get;}
  public TrackedFrame(string device="camera",bool typed=true){IVisionImage image;if(typed){_native=HalImage.CreateFromBytes(new byte[]{30,80},2,1,1);image=Substitute.For<IVisionImage,ITypedImageSnapshotSource>();((ITypedImageSnapshotSource)image).CaptureSnapshot(Arg.Any<ImageFrameAddress>()).Returns(call=>((ITypedImageSnapshotSource)_native).CaptureSnapshot(call.Arg<ImageFrameAddress>()));}else image=Substitute.For<IVisionImage>();image.When(x=>x.Dispose()).Do(_=>{ImageDisposals++;_native?.Dispose();});Frame=new(new(device),42,DateTimeOffset.Parse("2026-10-28T01:02:03+00:00"),image);}
  public void Dispose()=>Frame.Dispose();
 }
}
