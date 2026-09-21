using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public interface ICameraParameterProvider
{
    Task<CameraParameterSchema> GetSchemaAsync(
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, object?>> ReadValuesAsync(
        IReadOnlyCollection<string> names,
        CancellationToken cancellationToken);

    Task ApplyValuesAsync(
        IReadOnlyDictionary<string, object?> values,
        CancellationToken cancellationToken);
}
