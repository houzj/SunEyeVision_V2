using System.Collections.Generic;

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
    public bool RequiresStopped { get; init; }
    public string? Description { get; init; }
}

public sealed record CameraParameterSchema
{
    public string AdapterId { get; init; } = string.Empty;
    public string SchemaVersion { get; init; } = "1.0";
    public IReadOnlyList<CameraParameterDescriptor> Parameters { get; init; }
        = new List<CameraParameterDescriptor>();
}
