using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Simulation;
using SunEyeVision.Plugin.Contracts.Acquisition;
using Xunit;

namespace SunEyeVision.DeviceDriver.Tests;

public sealed class CameraRuntimeManagerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Workflow_capture_restores_exposure_and_gain_after_success_or_failure(bool fail)
    {
        var options=new SimulatedCameraOptions
        {
            DeviceId="sim-frame-parameters",
            FrameDelay=System.TimeSpan.Zero,
            FailCapture=fail
        };
        await using var manager=new CameraRuntimeManager(new[]{new SimulatedCameraFactory(options)});
        await manager.DiscoverAsync(new[]{new SimulatedCameraDiscoveryProvider(options)});
        await using var session=await manager.OpenAsync(new CameraDeviceRef(options.DeviceId),CameraAccessMode.Exclusive);
        var names=new[]{"ExposureTime","Gain"};
        await session.Adapter.ApplyParametersAsync(new System.Collections.Generic.Dictionary<string,object?>
        {["ExposureTime"]=1250d,["Gain"]=2d},CancellationToken.None);
        var run=new SunEyeVision.Plugin.Contracts.RunIdentity(System.Guid.NewGuid(),System.Guid.NewGuid(),System.Guid.NewGuid(),"source",System.Guid.NewGuid());
        var request=new CameraFrameRequest(options.DeviceId,CameraRequestPurpose.Workflow,run,System.TimeSpan.FromSeconds(1),
            exposureTime:750d,gain:4d);
        if(fail)
            Assert.Equal(CameraErrorCode.FrameTimeout,(await Assert.ThrowsAsync<CameraFrameAcquisitionException>(()=>manager.CaptureAsync(request,CancellationToken.None).AsTask())).ErrorCode);
        else
        {
            await using var frame=await manager.CaptureAsync(request,CancellationToken.None);
            Assert.Equal(run,frame.Image.Frame.ProducerRun);
        }
        var restored=await session.Adapter.ReadParametersAsync(names,CancellationToken.None);
        Assert.Equal(1250d,restored["ExposureTime"]);
        Assert.Equal(2d,restored["Gain"]);
    }

    [Fact]
    public async Task DiscoveryRegistersSimulationDeviceAndCaptureReturnsFrame()
    {
        var options = new SimulatedCameraOptions
        {
            DeviceId = "sim-test-001",
            FrameDelay = System.TimeSpan.Zero
        };
        var discovery = new SimulatedCameraDiscoveryProvider(options);
        var manager = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });

        var devices = await manager.DiscoverAsync(new[] { discovery });
        await using var frame = await manager.CaptureAsync(new CameraFrameRequest(options.DeviceId,CameraRequestPurpose.Preview,null,System.TimeSpan.FromSeconds(1)),CancellationToken.None);

        Assert.Single(devices);
        Assert.Equal(options.DeviceId, frame.DeviceId);
        Assert.True(frame.Image.Width > 0);
        Assert.True(frame.Image.Height > 0);

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task RuntimeManagerRejectsSecondExclusiveSession()
    {
        var options = new SimulatedCameraOptions { DeviceId = "sim-test-002" };
        var manager = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });
        await manager.DiscoverAsync(new[] { new SimulatedCameraDiscoveryProvider(options) });

        await using var first = await manager.OpenAsync(
            new CameraDeviceRef(options.DeviceId),
            CameraAccessMode.Exclusive);

        await Assert.ThrowsAsync<System.InvalidOperationException>(() => manager.OpenAsync(
            new CameraDeviceRef(options.DeviceId),
            CameraAccessMode.Exclusive));

        await manager.DisposeAsync();
    }

    [Fact]
    public async Task DisposingSessionReleasesRuntimeReservation()
    {
        var options = new SimulatedCameraOptions { DeviceId = "sim-test-003" };
        var manager = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });
        await manager.DiscoverAsync(new[] { new SimulatedCameraDiscoveryProvider(options) });

        var reference = new CameraDeviceRef(options.DeviceId);
        var first = await manager.OpenAsync(reference, CameraAccessMode.Exclusive);
        await first.DisposeAsync();

        await using var second = await manager.OpenAsync(reference, CameraAccessMode.Exclusive);
        Assert.Equal(reference, second.CameraRef);
        await manager.DisposeAsync();
    }

    [Fact]
    public async Task CaptureReusesExistingSessionWithoutDisconnectingIt()
    {
        var options = new SimulatedCameraOptions
        {
            DeviceId = "sim-test-reuse",
            FrameDelay = System.TimeSpan.Zero
        };
        await using var manager = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });
        await manager.DiscoverAsync(new[] { new SimulatedCameraDiscoveryProvider(options) });
        var cameraRef = new CameraDeviceRef(options.DeviceId);
        await using var session = await manager.OpenAsync(cameraRef, CameraAccessMode.Exclusive);

        await using var frame = await manager.CaptureAsync(new CameraFrameRequest(cameraRef.Value,CameraRequestPurpose.Preview,null,System.TimeSpan.FromSeconds(1)),CancellationToken.None);

        Assert.True(frame.Image.Width > 0);
        Assert.True(manager.TryGetSession(cameraRef, out var retained));
        Assert.Same(session, retained);
    }

    [Fact]
    public async Task PreviewAndWorkflowCannotOwnAcquisitionAtTheSameTime()
    {
        var options = new SimulatedCameraOptions { DeviceId = "sim-capture-lease" };
        await using var manager = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });
        var cameraRef = new CameraDeviceRef(options.DeviceId);

        using (manager.AcquireCaptureLease(cameraRef, "相机设置预览"))
        {
            var error = Assert.Throws<System.InvalidOperationException>(() =>
                manager.AcquireCaptureLease(cameraRef, "工作流"));
            Assert.Contains("相机设置预览", error.Message);
        }

        using var workflowLease = manager.AcquireCaptureLease(cameraRef, "工作流");
        Assert.Throws<System.InvalidOperationException>(() =>
            manager.AcquireCaptureLease(cameraRef, "相机设置预览"));
    }
}
