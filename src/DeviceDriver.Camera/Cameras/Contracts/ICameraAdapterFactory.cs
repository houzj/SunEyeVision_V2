using SunEyeVision.Plugin.Contracts.Acquisition;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public interface ICameraAdapterFactory
{
    string AdapterId { get; }
    bool CanCreate(CameraDescriptor descriptor);
    bool SupportsWorkflowFrames(CameraDescriptor descriptor);
    ICameraAdapter Create(CameraDescriptor descriptor);
}
