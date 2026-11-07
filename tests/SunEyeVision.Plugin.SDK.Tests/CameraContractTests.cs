using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Simulation;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;
using Xunit;
namespace SunEyeVision.Plugin.Editing.Wpf.Tests;
public sealed class CameraContractTests
{
 [Fact] public async Task CameraLease_DisposeIsIdempotent_AndReaderSurvives()
 {
  var options=new SimulatedCameraOptions{DeviceId="contract-camera",Width=2,Height=1,FrameDelay=TimeSpan.Zero};
  await using var manager=new CameraRuntimeManager(new[]{new SimulatedCameraFactory(options)});
  manager.RegisterDevices(await new SimulatedCameraDiscoveryProvider(options).DiscoverAsync(CancellationToken.None));
  var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"source",Guid.NewGuid());
  var frame=await manager.CaptureAsync(new CameraFrameRequest(options.DeviceId,CameraRequestPurpose.Workflow,run,TimeSpan.FromSeconds(1)),CancellationToken.None);
  using var reader=frame.Image.AcquirePixels();await frame.DisposeAsync();await frame.DisposeAsync();
  Assert.Equal(2,reader.GetRow(0).Length);Assert.Throws<ObjectDisposedException>(()=>frame.Image);
 }
 [Fact] public void CaptureRequest_DefaultsToPreviewAndSoftwareTrigger()
 {
  var request=new CameraCaptureRequest();Assert.Equal(CameraTriggerMode.Software,request.TriggerMode);
  Assert.Equal(CameraRequestPurpose.Preview,request.Purpose);Assert.Null(request.ProducerRun);Assert.True(request.Timeout>TimeSpan.Zero);Assert.Empty(request.Parameters);
 }
 [Fact] public void CameraDescriptor_PreservesStableIdentityAndTransport()
 {
  var descriptor=new CameraDescriptor{DeviceId="industrial-camera:ABC123",CameraRef=new CameraDeviceRef("industrial-camera:ABC123"),Manufacturer="Hikvision",SerialNumber="ABC123",Transport=CameraTransportKind.GigEVision,Protocol=CameraProtocolKind.GenICam};
  Assert.Equal("ABC123",descriptor.SerialNumber);Assert.Equal(CameraTransportKind.GigEVision,descriptor.Transport);Assert.Equal(CameraProtocolKind.GenICam,descriptor.Protocol);
 }
 [Fact] public void RequestIdentity_DistinguishesPreviewAndActualWorkflow()
 {
  var run=new RunIdentity(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"source",Guid.NewGuid());
  var request=new CameraFrameRequest("camera",CameraRequestPurpose.Workflow,run,TimeSpan.FromSeconds(1));Assert.Equal(run,request.ProducerRun);
  Assert.Throws<ArgumentException>(()=>new CameraFrameRequest("camera",CameraRequestPurpose.Preview,run,TimeSpan.FromSeconds(1)));
  Assert.Throws<ArgumentException>(()=>new CameraFrameRequest("camera",CameraRequestPurpose.Workflow,null,TimeSpan.FromSeconds(1)));
 }
}
