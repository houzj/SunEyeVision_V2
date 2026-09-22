using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public interface ICameraDiscoveryProvider
{
    string ProviderId { get; }

    Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(
        CancellationToken cancellationToken,
        IReadOnlySet<CameraTransportKind>? transports = null);
}
