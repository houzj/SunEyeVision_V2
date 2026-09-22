using System;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public sealed class CameraAdapterException : Exception
{
    public CameraAdapterException(CameraErrorCode errorCode, string message)
        : base(message) => ErrorCode = errorCode;

    public CameraErrorCode ErrorCode { get; }
}
