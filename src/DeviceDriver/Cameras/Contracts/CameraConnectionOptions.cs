using System;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public sealed record CameraConnectionOptions
{
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool ApplyParameterSnapshotOnConnect { get; init; } = true;
}
