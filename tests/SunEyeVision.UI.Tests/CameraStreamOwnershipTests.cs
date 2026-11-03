using NSubstitute;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestShared;
using SunEyeVision.UI.Features.Camera;
using System.Windows;
using System.Windows.Threading;
using Xunit;
namespace SunEyeVision.UI.Tests;
[Collection("Editor commit UI")]
public sealed class CameraStreamOwnershipTests
{
 private sealed class ControlledCamera:IAsyncDisposable
 {
  internal readonly ICameraAdapter Adapter=Substitute.For<ICameraAdapter>();
  internal readonly ICameraAdapterFactory Factory=Substitute.For<ICameraAdapterFactory>();
  internal readonly TaskCompletionSource Stream=new(TaskCreationOptions.RunContinuationsAsynchronously);
  internal readonly CameraDeviceRef Reference=new("controlled-stream");
  internal readonly CameraRuntimeManager Runtime;internal CameraSession Session=null!;
  internal ControlledCamera()
  {
   Factory.CanCreate(Arg.Any<CameraDescriptor>()).Returns(true);Factory.SupportsWorkflowFrames(Arg.Any<CameraDescriptor>()).Returns(true);Factory.Create(Arg.Any<CameraDescriptor>()).Returns(Adapter);
   Adapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(),Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);Adapter.State.Returns(CameraState.Connected);
   Adapter.ReadCaptureConfigurationAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CameraCaptureConfiguration()));
   Adapter.GetParameterSchemaAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(new CameraParameterSchema()));
   Adapter.StartStreamAsync(Arg.Any<CameraCaptureRequest>(),Arg.Any<ICameraFrameSink>(),Arg.Any<CancellationToken>()).Returns(_=>Stream.Task);
   Adapter.StopStreamAsync(Arg.Any<CancellationToken>()).Returns(_=>{Stream.TrySetResult();return Task.CompletedTask;});Adapter.DisposeAsync().Returns(ValueTask.CompletedTask);
   Runtime=new(new[]{Factory});Runtime.RegisterDevice(new CameraDescriptor{DeviceId=Reference.Value,CameraRef=Reference});
  }
  internal async Task OpenAsync()=>Session=await Runtime.OpenAsync(Reference,CameraAccessMode.Exclusive);
  public async ValueTask DisposeAsync(){Stream.TrySetResult();await Runtime.DisposeAsync();}
 }
 private static void Pump(Func<Task> action)
 {
  var frame=new DispatcherFrame();Exception? failure=null;_ = Run();Dispatcher.PushFrame(frame);if(failure is not null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
  async Task Run(){try{await action().WaitAsync(TimeSpan.FromSeconds(15));}catch(Exception error){failure=error;}finally{frame.Continue=false;}}
 }
 [Fact] public void Preview_successful_stop_drains_stream_before_workflow_can_reuse_borrowed_session()=>WpfTestDispatcher.Invoke(()=>Pump(async()=>
 {
  await using var camera=new ControlledCamera();await camera.OpenAsync();var host=new CameraPreviewHost(camera.Runtime,camera.Reference,camera.Session);
  await host.RefreshConfigurationAsync();await host.StartAcquisitionAsync();Assert.True(host.IsAcquiring);
  await host.StopAcquisitionAsync();Assert.False(host.IsAcquiring);using(camera.Runtime.AcquireCaptureLease(camera.Reference,"工作流")){}
  await host.DisposeAsync();Assert.True(camera.Runtime.TryGetSession(camera.Reference,out var actual));Assert.Same(camera.Session,actual);
 }));
 [Fact] public void Closing_during_inflight_open_waits_and_releases_only_the_session_it_opened()=>WpfTestDispatcher.Invoke(()=>Pump(async()=>
 {
  await using var camera=new ControlledCamera();var connecting=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);camera.Adapter.ConnectAsync(Arg.Any<CameraConnectionOptions>(),Arg.Any<CancellationToken>()).Returns(_=>connecting.Task);
  var host=new CameraPreviewHost(camera.Runtime,camera.Reference);var refresh=host.RefreshConfigurationAsync();var close=host.DisposeAsync().AsTask();var again=host.DisposeAsync().AsTask();Assert.False(close.IsCompleted);Assert.False(again.IsCompleted);
  connecting.SetResult();await close;await again;await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>refresh);Assert.False(camera.Runtime.TryGetSession(camera.Reference,out _));_ = camera.Adapter.Received(1).DisposeAsync();
 }));
 // The former converter boundary is retired: exercise the same driver-thread ownership path
 // with a neutral preview-only domainless frame, including late delivery after close.
 [Fact] public void Preview_null_conversion_from_driver_thread_is_marshaled_and_releases_frame()=>WpfTestDispatcher.Invoke(()=>Pump(async()=>
 {
  await using var camera=new ControlledCamera();await camera.OpenAsync();var host=new CameraPreviewHost(camera.Runtime,camera.Reference,camera.Session);var window=new Window{Content=host,Left=-32000,Top=-32000,ShowActivated=false,ShowInTaskbar=false};
  try{window.Show();var frame=new Frame(camera.Reference.Value);await Task.Run(async()=>await host.OnFrameAsync(frame,CancellationToken.None));Assert.True(frame.IsDisposed);await host.DisposeAsync();var late=new Frame(camera.Reference.Value);await host.OnFrameAsync(late,CancellationToken.None);Assert.True(late.IsDisposed);}
  finally{await host.DisposeAsync();window.Close();}
 }));
 private sealed class Frame(string device):ICameraFrameLease
 {
  private FrozenImageResource? _image=new(new ImageLayout(2,1,8,PixelLayout.Bgra32),new ImageFrameAddress(Guid.NewGuid(),"preview"),new CoordinateFrame("preview",CoordinateDomain.Image,CoordinateUnit.Pixel),new byte[]{1,2,3,255,4,5,6,255},Array.Empty<RegionRun>());
  public string DeviceId=>device;public long DeviceFrameId=>1;public DateTimeOffset Timestamp=>DateTimeOffset.UtcNow;public IReadOnlyImage Image=>_image??throw new ObjectDisposedException(nameof(Frame));public bool IsDisposed=>_image is null;
  public ValueTask DisposeAsync(){Interlocked.Exchange(ref _image,null)?.Dispose();return ValueTask.CompletedTask;}
 }
}
