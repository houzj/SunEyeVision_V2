using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;

namespace SunEyeVision.DeviceDriver.Cameras.Industrial;

public sealed class IndustrialCameraAdapterFactory : ICameraAdapterFactory
{
    private readonly IndustrialCameraDiscoveryProvider _provider;
    private readonly Action<CameraDiagnostic>? _diagnostics;

    public IndustrialCameraAdapterFactory(IndustrialCameraDiscoveryProvider provider, Action<CameraDiagnostic>? diagnostics = null)
    {
        _provider = provider;
        _diagnostics=diagnostics;
    }

    public string AdapterId => "industrial-camera";

    public bool CanCreate(CameraDescriptor descriptor)
        => descriptor.Protocol == CameraProtocolKind.GenICam
           && (string.Equals(descriptor.Manufacturer, "Hikvision", System.StringComparison.OrdinalIgnoreCase)
               || string.Equals(descriptor.Manufacturer, "Hikrobot", System.StringComparison.OrdinalIgnoreCase)
               || descriptor.DeviceId.StartsWith("industrial-camera:", System.StringComparison.OrdinalIgnoreCase)
               || (descriptor.Metadata.TryGetValue("adapter", out var adapter)
                   && string.Equals(adapter as string, AdapterId, System.StringComparison.OrdinalIgnoreCase)));

    public bool SupportsWorkflowFrames(CameraDescriptor descriptor) => false;
    public ICameraAdapter Create(CameraDescriptor descriptor)
    {
#if MVS_SDK_AVAILABLE
        if (!_provider.TryGetDeviceInfo(descriptor.DeviceId, out var info))
        {
            throw new CameraAdapterException(CameraErrorCode.CameraNotFound, "设备已离线或尚未重新发现");
        }

        return new IndustrialCameraAdapter(descriptor, info, _diagnostics);
#else
        throw new CameraAdapterException(CameraErrorCode.DriverUnavailable, "未找到海康 MVS SDK");
#endif
    }
}
