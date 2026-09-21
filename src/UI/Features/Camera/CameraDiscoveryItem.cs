using SunEyeVision.Plugin.SDK.Devices.Camera;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Features.Camera;

/// <summary>
/// 自动发现结果的 UI 投影。设备描述仍由 SDK 契约提供，UI 只维护选择状态。
/// </summary>
public sealed class CameraDiscoveryItem : ObservableObject
{
    public string Name { get; init; } = string.Empty;
    public string Manufacturer { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string SerialNumber { get; init; } = string.Empty;
    public string? MacAddress { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public CameraTransportKind Transport { get; init; }
    public bool IsAvailable { get; init; } = true;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
