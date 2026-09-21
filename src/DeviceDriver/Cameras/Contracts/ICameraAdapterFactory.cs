using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public interface ICameraAdapterFactory
{
    string AdapterId { get; }
    bool CanCreate(CameraDescriptor descriptor);
    ICameraAdapter Create(CameraDescriptor descriptor);
}
