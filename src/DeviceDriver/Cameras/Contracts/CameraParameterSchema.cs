using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

public enum CameraParameterKind
{
    Boolean,
    Integer,
    Double,
    String,
    Enumeration,
    Command,
    Rectangle,
    Custom
}

public sealed record CameraEnumValue(string Value, string DisplayName);

public sealed record CameraParameterDescriptor
{
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    /// <summary>参数在适配器枚举到的原生 XML 功能树中的顺序。</summary>
    public int SourceOrder { get; init; } = int.MaxValue;
    /// <summary>稳定的跨适配器功能标识；厂商私有节点可为空。</summary>
    public string? StandardFeature { get; init; }
    public string Category { get; init; } = string.Empty;
    public CameraParameterKind Kind { get; init; }
    public object? Value { get; init; }
    public object? Minimum { get; init; }
    public object? Maximum { get; init; }
    public object? Increment { get; init; }
    public IReadOnlyList<CameraEnumValue> EnumValues { get; init; }
        = new List<CameraEnumValue>();
    public bool IsReadOnly { get; init; }
    public bool IsAvailable { get; init; } = true;
    public bool IsReadable { get; init; } = true;
    public bool IsWritable { get; init; } = true;
    public bool IsImplemented { get; init; } = true;
    public bool RequiresStopped { get; init; }
    public bool AffectsSchema { get; init; }
    public string? Visibility { get; init; }
    public string? StatusMessage { get; init; }
    public string? Description { get; init; }
}

public sealed record CameraParameterSchema
{
    public string AdapterId { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "1.0";
    public IReadOnlyList<CameraParameterDescriptor> Parameters { get; init; }
        = new List<CameraParameterDescriptor>();
}

/// <summary>适配器将厂商参数解释为通用采集行为后提供给预览界面。</summary>
public sealed record CameraCaptureConfiguration
{
    public CameraTriggerMode StreamMode { get; init; } = CameraTriggerMode.Continuous;
    public string AcquisitionModeName { get; init; } = "未知";
    public string TriggerSourceName { get; init; } = string.Empty;
    public bool IsSingleFrame { get; init; }
    public bool IsValid { get; init; } = true;
    public string? StatusMessage { get; init; }
}
