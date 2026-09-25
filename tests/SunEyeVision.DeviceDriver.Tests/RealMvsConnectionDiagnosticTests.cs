using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;
using SunEyeVision.Plugin.SDK.Devices.Camera;
using Xunit;
using Xunit.Abstractions;

namespace SunEyeVision.DeviceDriver.Tests;

/// <summary>
/// 只读诊断：枚举指定真实相机、打开会话、立即关闭。
/// 不采集图像、不读写参数，也不修改方案文件。
/// </summary>
public sealed class RealMvsConnectionDiagnosticTests
{
    private const string ExpectedSerial = "DA5892282";
    private readonly ITestOutputHelper _output;

    public RealMvsConnectionDiagnosticTests(ITestOutputHelper output) => _output = output;

    [Fact(Timeout = 30000)]
    [Trait("Category", "RealHardware")]
    public async Task DiscoverAndOpenPersistedHikrobotCamera()
    {
        var provider = new MvsCameraDiscoveryProvider();
        var elapsed = Stopwatch.StartNew();
        _output.WriteLine($"DISCOVERY_BEGIN serial={ExpectedSerial}");

        var devices = await provider.DiscoverAsync(
            CancellationToken.None,
            new System.Collections.Generic.HashSet<CameraTransportKind> { CameraTransportKind.GigEVision });

        _output.WriteLine($"DISCOVERY_END elapsedMs={elapsed.ElapsedMilliseconds} count={devices.Count}");
        foreach (var device in devices)
            _output.WriteLine($"DEVICE id={device.DeviceId} serial={device.SerialNumber} ip={device.IpAddress} transport={device.Transport}");

        var matching = devices.Where(device =>
            string.Equals(device.SerialNumber, ExpectedSerial, StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.Single(matching);
        var descriptor = matching[0];
        Assert.True(string.Equals($"hikvision-mvs:{ExpectedSerial}", descriptor.DeviceId,
            StringComparison.OrdinalIgnoreCase), $"Unexpected DeviceId: {descriptor.DeviceId}");

        var factory = new MvsCameraAdapterFactory(provider);
        Assert.True(factory.CanCreate(descriptor), "MVS factory rejected the discovered camera descriptor.");
        _output.WriteLine($"MATCH_OK elapsedMs={elapsed.ElapsedMilliseconds} id={descriptor.DeviceId}");

        await using var runtime = new CameraRuntimeManager(new[] { factory });
        runtime.RegisterDevice(descriptor);
        var openedAt = elapsed.ElapsedMilliseconds;
        _output.WriteLine($"OPEN_BEGIN elapsedMs={openedAt} id={descriptor.DeviceId}");
        try
        {
            var session = await runtime.OpenAsync(
                descriptor.CameraRef,
                CameraAccessMode.Diagnostic,
                new CameraConnectionOptions { Timeout = TimeSpan.FromSeconds(5) });
            _output.WriteLine($"OPEN_OK elapsedMs={elapsed.ElapsedMilliseconds - openedAt} state={session.Adapter.State}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"OPEN_FAILED elapsedMs={elapsed.ElapsedMilliseconds - openedAt} type={ex.GetType().FullName} message={ex.Message}");
            throw;
        }

        _output.WriteLine($"CLOSE_BEGIN elapsedMs={elapsed.ElapsedMilliseconds}");
        await runtime.CloseAsync(descriptor.CameraRef);
        _output.WriteLine($"CLOSE_OK elapsedMs={elapsed.ElapsedMilliseconds}");
    }
}
