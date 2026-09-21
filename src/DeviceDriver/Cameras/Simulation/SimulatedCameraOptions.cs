using System;

namespace SunEyeVision.DeviceDriver.Cameras.Simulation;

public sealed record SimulatedCameraOptions
{
    public string DeviceId { get; init; } = "sim-camera-001";
    public string DisplayName { get; init; } = "模拟相机";
    public int Width { get; init; } = 640;
    public int Height { get; init; } = 480;
    public double FrameRate { get; init; } = 30;
    public TimeSpan ConnectDelay { get; init; } = TimeSpan.Zero;
    public TimeSpan FrameDelay { get; init; } = TimeSpan.FromMilliseconds(33);
    public bool FailConnection { get; init; }
    public bool FailCapture { get; init; }
    public int DropEveryNthFrame { get; init; }
}
