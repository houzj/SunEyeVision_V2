using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Simulation;

public sealed class SimulatedCameraDiscoveryProvider : ICameraDiscoveryProvider
{
    private readonly SimulatedCameraOptions _options;

    public SimulatedCameraDiscoveryProvider(SimulatedCameraOptions? options = null)
    {
        _options = options ?? new SimulatedCameraOptions();
    }

    public string ProviderId => "simulation";

    public Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CameraDescriptor> result = new[]
        {
            new CameraDescriptor
            {
                DeviceId = _options.DeviceId,
                CameraRef = new CameraDeviceRef(_options.DeviceId),
                DisplayName = _options.DisplayName,
                Manufacturer = "SunEyeVision",
                Model = "SimulatedCamera",
                SerialNumber = _options.DeviceId,
                Transport = CameraTransportKind.Simulation,
                Protocol = CameraProtocolKind.Simulation
            }
        };

        return Task.FromResult(result);
    }
}
