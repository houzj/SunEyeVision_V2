using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.Contracts.Acquisition;

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
    public void Validate()
    {
        if(CameraRef is null || CameraRef.IsEmpty)throw new ArgumentException("相机身份不能为空。",nameof(CameraRef));
        if(!Enum.IsDefined(Purpose) || Purpose==CameraRequestPurpose.Workflow && ProducerRun is null || Purpose==CameraRequestPurpose.Preview && ProducerRun is not null)
            throw new ArgumentException("采集用途与运行身份不一致。",nameof(ProducerRun));
        _ = new CameraFrameRequest(CameraRef.Value,Purpose,ProducerRun,Timeout,DiagnosticCorrelationId);
    }
    public CameraDeviceRef CameraRef { get; init; } = new(string.Empty);
    public RunIdentity? ProducerRun { get; init; }
    public CameraRequestPurpose Purpose { get; init; } = CameraRequestPurpose.Preview;
    public CameraTriggerMode TriggerMode { get; init; } = CameraTriggerMode.Software;
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(3);
    /// <summary>Optional host correlation id for joining parameter, preview, and adapter diagnostics.</summary>
    public string? DiagnosticCorrelationId { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; }
        = new Dictionary<string, object?>();
}
