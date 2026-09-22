namespace SunEyeVision.Plugin.SDK.Devices.Camera;

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
