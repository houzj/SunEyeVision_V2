using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Devices.Camera;

public enum CameraTriggerMode
{
    Software,
    Hardware,
    Continuous
}

/// <summary>
/// 一次采集请求。参数是运行开始时解析后的本次请求值，不是可变的设备全局状态。
/// </summary>
public sealed record CameraCaptureRequest
{
    public CameraDeviceRef CameraRef { get; init; } = new(string.Empty);
    public CameraTriggerMode TriggerMode { get; init; } = CameraTriggerMode.Software;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
    /// <summary>Optional host correlation id for joining parameter, preview, and adapter diagnostics.</summary>
    public string? DiagnosticCorrelationId { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
        = new Dictionary<string, object?>();
}
