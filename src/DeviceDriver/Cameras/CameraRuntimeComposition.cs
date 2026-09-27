using System;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;

namespace SunEyeVision.DeviceDriver.Cameras;

/// <summary>
/// 默认相机运行时装配。宿主只需要持有该对象，不需要知道厂商 SDK 类型。
/// </summary>
public sealed class CameraRuntimeComposition : IAsyncDisposable
{
    public CameraRuntimeComposition()
    {
        MvsDiscovery = new MvsCameraDiscoveryProvider();
        Runtime = new CameraRuntimeManager(new ICameraAdapterFactory[]
        {
            new MvsCameraAdapterFactory(MvsDiscovery)
        });
        Discovery = new CameraDiscoveryCoordinator(new ICameraDiscoveryProvider[]
        {
            MvsDiscovery
        });
    }

    public CameraRuntimeManager Runtime { get; }
    public CameraDiscoveryCoordinator Discovery { get; }
    public MvsCameraDiscoveryProvider MvsDiscovery { get; }

    public async ValueTask DisposeAsync() => await Runtime.DisposeAsync().ConfigureAwait(false);
}
