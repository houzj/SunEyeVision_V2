using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.Contracts.Acquisition;

namespace SunEyeVision.DeviceDriver.Cameras.Simulation;

public sealed class SimulatedCameraFactory : ICameraAdapterFactory
{
    private readonly SimulatedCameraOptions _options;

    public SimulatedCameraFactory(SimulatedCameraOptions? options = null)
    {
        _options = options ?? new SimulatedCameraOptions();
    }

    public string AdapterId => "simulation";

    public bool CanCreate(CameraDescriptor descriptor)
        => descriptor.Protocol == CameraProtocolKind.Simulation
           || descriptor.Transport == CameraTransportKind.Simulation;

    public bool SupportsWorkflowFrames(CameraDescriptor descriptor) => CanCreate(descriptor);
    public ICameraAdapter Create(CameraDescriptor descriptor)
        => new SimulatedCameraAdapter(descriptor, _options);
}
