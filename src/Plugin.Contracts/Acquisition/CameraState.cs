namespace SunEyeVision.Plugin.Contracts.Acquisition;

public enum CameraState
{
    Offline,
    Connecting,
    Connected,
    StartingCapture,
    Capturing,
    StoppingCapture,
    Reconnecting,
    Faulted,
    Disposed
}
