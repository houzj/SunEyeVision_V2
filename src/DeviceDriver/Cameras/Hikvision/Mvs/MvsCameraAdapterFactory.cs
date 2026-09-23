using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;

public sealed class MvsCameraAdapterFactory : ICameraAdapterFactory
{
    private readonly MvsCameraDiscoveryProvider _provider;

    public MvsCameraAdapterFactory(MvsCameraDiscoveryProvider provider)
    {
        _provider = provider;
    }

    public string AdapterId => "hikvision-mvs";

    public bool CanCreate(CameraDescriptor descriptor)
        => descriptor.Protocol == CameraProtocolKind.GenICam
           && (string.Equals(descriptor.Manufacturer, "Hikvision", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(descriptor.Manufacturer, "Hikrobot", System.StringComparison.OrdinalIgnoreCase)
               || descriptor.DeviceId.StartsWith("hikvision-mvs:", System.StringComparison.OrdinalIgnoreCase)
               || (descriptor.Metadata.TryGetValue("adapter", out var adapter)
                   && string.Equals(adapter as string, AdapterId, System.StringComparison.OrdinalIgnoreCase)));

    public ICameraAdapter Create(CameraDescriptor descriptor)
    {
#if MVS_SDK_AVAILABLE
        if (!_provider.TryGetDeviceInfo(descriptor.DeviceId, out var info))
        {
            throw new CameraAdapterException(CameraErrorCode.CameraNotFound, "设备已离线或尚未重新发现");
        }

        return new MvsCameraAdapter(descriptor, info);
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }
}
