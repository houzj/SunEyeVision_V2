using System;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Industrial;

namespace SunEyeVision.DeviceDriver.Cameras;

/// <summary>
/// 默认相机运行时装配。宿主只需要持有该对象，不需要知道厂商 SDK 类型。
/// </summary>
public sealed class CameraRuntimeComposition : IAsyncDisposable
{
    public CameraRuntimeComposition(Action<CameraDiagnostic>? diagnostics = null)
    {
        IndustrialDiscovery = new IndustrialCameraDiscoveryProvider();
        Runtime = new CameraRuntimeManager(new ICameraAdapterFactory[]
        {
            new IndustrialCameraAdapterFactory(IndustrialDiscovery,diagnostics)
        });
        Discovery = new CameraDiscoveryCoordinator(new ICameraDiscoveryProvider[]
        {
            IndustrialDiscovery
        });
    }

    public CameraRuntimeManager Runtime { get; }
    public CameraDiscoveryCoordinator Discovery { get; }
    public IndustrialCameraDiscoveryProvider IndustrialDiscovery { get; }

    public async ValueTask DisposeAsync() => await Runtime.DisposeAsync().ConfigureAwait(false);
}
