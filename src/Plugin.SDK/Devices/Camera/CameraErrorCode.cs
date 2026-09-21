namespace SunEyeVision.Plugin.SDK.Devices.Camera;

public enum CameraErrorCode
{
    None,
    CameraNotFound,
    ConnectionTimeout,
    ConnectionRejected,
    DeviceBusy,
    TriggerTimeout,
    FrameTimeout,
    ParameterUnsupported,
    StreamStopped,
    DeviceDisconnected,
    DriverUnavailable,
    OperationCancelled,
    Unknown
}
