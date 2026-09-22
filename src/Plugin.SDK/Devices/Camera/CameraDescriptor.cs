using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Devices.Camera;

public enum CameraTransportKind
{
    Unknown,
    GigEVision,
    Usb3Vision,
    CameraLink,
    GenTL,
    DirectShow,
    MediaFoundation,
    Simulation,
    File
}

public enum CameraProtocolKind
{
    Unknown,
    GenICam,
    GigEVision,
    Usb3Vision,
    CameraLink,
    VendorSdk,
    Simulation,
    File
}

/// <summary>
/// 对 Workflow、插件和 UI 公开的设备描述。
/// </summary>
public sealed record CameraDescriptor
{
    public string DeviceId { get; init; } = string.Empty;
    public CameraDeviceRef CameraRef { get; init; } = new(string.Empty);
    public string DisplayName { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string? IpAddress { get; init; }
    public string? MacAddress { get; init; }
    public CameraTransportKind Transport { get; init; }
    public CameraProtocolKind Protocol { get; init; }
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();
}
