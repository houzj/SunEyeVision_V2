using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Simulation;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using Xunit;

namespace SunEyeVision.DeviceDriver.Tests;

public sealed class CameraRuntimeManagerTests
{
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
        using var frame = await manager.CaptureAsync(
            new CameraCaptureRequest
            {
                CameraRef = new CameraDeviceRef(options.DeviceId),
                Timeout = System.TimeSpan.FromSeconds(1)
            },
            CancellationToken.None);

        Assert.Single(devices);
        Assert.Equal(options.DeviceId, frame.CameraRef.Value);
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
}
