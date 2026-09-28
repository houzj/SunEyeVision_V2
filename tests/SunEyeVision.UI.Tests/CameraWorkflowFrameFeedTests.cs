using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Simulation;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.UI.Services.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CameraWorkflowFrameFeedTests
{
    [Fact]
    public async Task FreeRunningCameraSuppliesOnePreboundFrameAndKeepsSessionConnected()
    {
        var options = new SimulatedCameraOptions
        {
            DeviceId = "sim-workflow-feed",
            FrameDelay = TimeSpan.FromMilliseconds(10)
        };
        await using var runtime = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });
        await runtime.DiscoverAsync(new[] { new SimulatedCameraDiscoveryProvider(options) });
        var cameraRef = new CameraDeviceRef(options.DeviceId);
        await using var session = await runtime.OpenAsync(cameraRef, CameraAccessMode.Exclusive);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using (var feed = await CameraWorkflowFrameFeed.StartAsync(
            runtime, cameraRef, TimeSpan.FromSeconds(1), deadline.Token))
        {
            using var frame = await feed.NextFrameAsync(deadline.Token);
            Assert.Equal(options.DeviceId, frame.CameraRef.Value);
            Assert.False(frame.Image.IsEmpty);
        }

        Assert.True(runtime.TryGetSession(cameraRef, out var retained));
        Assert.Same(session, retained);
    }

    [Fact]
    public async Task SoftwareTriggerProducesOneFramePerSchedulerRequest()
    {
        var options = new SimulatedCameraOptions
        {
            DeviceId = "sim-workflow-software",
            FrameDelay = TimeSpan.FromMilliseconds(10)
        };
        await using var runtime = new CameraRuntimeManager(new[] { new SimulatedCameraFactory(options) });
        await runtime.DiscoverAsync(new[] { new SimulatedCameraDiscoveryProvider(options) });
        var cameraRef = new CameraDeviceRef(options.DeviceId);
        await using var session = await runtime.OpenAsync(cameraRef, CameraAccessMode.Exclusive);
        await session.Adapter.ApplyParametersAsync(new Dictionary<string, object?>
        {
            ["TriggerMode"] = 1,
            ["TriggerSource"] = 7
        }, CancellationToken.None);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await using var feed = await CameraWorkflowFrameFeed.StartAsync(
            runtime, cameraRef, TimeSpan.FromSeconds(1), deadline.Token);
        Assert.Equal(CameraTriggerMode.Software, feed.Mode);
        Assert.Equal(0, feed.ReceivedFrames);

        using var first = await feed.NextFrameAsync(deadline.Token);
        Assert.Equal(1, feed.DeliveredFrames);
        Assert.Equal(1, feed.ReceivedFrames);
        using var second = await feed.NextFrameAsync(deadline.Token);
        Assert.True(second.FrameId > first.FrameId);
        Assert.Equal(2, feed.DeliveredFrames);
        Assert.Equal(2, feed.ReceivedFrames);
    }
}
