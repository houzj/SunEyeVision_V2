using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Devices.Camera;

/// <summary>
/// 一次运行使用的相机参数快照。
/// </summary>
public sealed record CameraParameterSnapshot
{
    public CameraDeviceRef CameraRef { get; init; } = new(string.Empty);
    public DateTimeOffset CapturedAt { get; init; }
    public IReadOnlyDictionary<string, object?> Values { get; init; }
        = new Dictionary<string, object?>();
}
