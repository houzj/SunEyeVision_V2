namespace SunEyeVision.Plugin.Contracts.Acquisition;

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
