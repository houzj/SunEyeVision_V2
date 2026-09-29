using System;
using System.Globalization;
using System.Linq;
using SunEyeVision.DeviceDriver.Cameras.Contracts;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Hikvision.Mvs;

/// <summary>Interprets each device's GenICam symbolic entries; numeric enum values are device-local.</summary>
public static class MvsCaptureConfigurationParser
{
    public static CameraCaptureConfiguration Parse(
        CameraParameterDescriptor? acquisition,
        CameraParameterDescriptor? triggerMode,
        CameraParameterDescriptor? triggerSource,
        CameraParameterDescriptor? triggerSelector = null)
    {
        if (!TryGetSymbolic(acquisition, out var acquisitionSymbol))
            return Invalid("无法识别设备采集模式");

        var acquisitionName = Normalize(acquisitionSymbol) switch
        {
            "SINGLEFRAME" => "单帧",
            "MULTIFRAME" => "多帧",
            "CONTINUOUS" => "连续",
            _ => null
        };
        if (acquisitionName is null)
            return Invalid($"不支持的采集模式：{acquisitionSymbol}");
        var singleFrame = acquisitionName == "单帧";

        if (!TryGetSymbolic(triggerMode, out var modeSymbol))
            return Invalid("无法识别设备触发模式", acquisitionName, singleFrame);

        var mode = Normalize(modeSymbol);
        if (mode is "OFF" or "DISABLED" or "FALSE")
            return new CameraCaptureConfiguration
            {
                StreamMode = CameraTriggerMode.Continuous,
                AcquisitionModeName = acquisitionName,
                IsSingleFrame = singleFrame,
                StatusMessage = "触发关闭"
            };
        if (mode is not ("ON" or "ENABLED" or "TRUE"))
            return Invalid($"不支持的触发模式：{modeSymbol}", acquisitionName, singleFrame);

        if (triggerSelector is not null)
        {
            if (!TryGetSymbolic(triggerSelector, out var selectorSymbol))
                return Invalid("无法识别触发选择器", acquisitionName, singleFrame);
            if (Normalize(selectorSymbol) != "FRAMESTART")
                return Invalid($"当前只支持 FrameStart 触发，设备为 {selectorSymbol}", acquisitionName, singleFrame);
        }

        if (!TryGetSymbolic(triggerSource, out var sourceSymbol))
            return Invalid("触发已开启，但无法识别触发源", acquisitionName, singleFrame);

        var source = Normalize(sourceSymbol);
        if (source == "SOFTWARE")
            return new CameraCaptureConfiguration
            {
                StreamMode = CameraTriggerMode.Software,
                AcquisitionModeName = acquisitionName,
                IsSingleFrame = singleFrame,
                TriggerSourceName = "软件触发"
            };

        if (source.StartsWith("LINE", StringComparison.Ordinal)
            && source.Length > 4 && source[4..].All(char.IsDigit))
            return Hardware($"线路 {source[4..]}", acquisitionName, singleFrame);
        if (new[] { "ACTION", "COUNTER", "TIMER", "ENCODER" }
            .Any(prefix => source.StartsWith(prefix, StringComparison.Ordinal)
                           && source.Length > prefix.Length
                           && source[prefix.Length..].All(char.IsDigit)))
            return Hardware(sourceSymbol, acquisitionName, singleFrame);

        return Invalid($"不支持的触发源：{sourceSymbol}", acquisitionName, singleFrame);
    }

    public static string Describe(CameraParameterDescriptor? descriptor)
    {
        var raw = Convert.ToString(descriptor?.Value, CultureInfo.InvariantCulture) ?? "<unavailable>";
        return TryGetSymbolic(descriptor, out var symbolic) ? $"{raw}/{symbolic}" : $"{raw}/<unknown>";
    }

    private static bool TryGetSymbolic(CameraParameterDescriptor? descriptor, out string symbolic)
    {
        symbolic = string.Empty;
        if (descriptor?.Kind != CameraParameterKind.Enumeration || descriptor.Value is null)
            return false;
        var raw = Convert.ToString(descriptor.Value, CultureInfo.InvariantCulture);
        var entry = descriptor.EnumValues.FirstOrDefault(value =>
            string.Equals(value.Value, raw, StringComparison.OrdinalIgnoreCase));
        if (entry is null || string.IsNullOrWhiteSpace(entry.DisplayName)
            || string.Equals(entry.DisplayName, raw, StringComparison.OrdinalIgnoreCase))
            return false;
        symbolic = entry.DisplayName.Trim();
        return true;
    }

    private static string Normalize(string value)
        => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static CameraCaptureConfiguration Hardware(string sourceName, string acquisitionName, bool singleFrame)
        => new()
        {
            StreamMode = CameraTriggerMode.Hardware,
            AcquisitionModeName = acquisitionName,
            IsSingleFrame = singleFrame,
            TriggerSourceName = sourceName
        };

    private static CameraCaptureConfiguration Invalid(string message, string acquisitionName = "未知", bool singleFrame = false)
        => new()
        {
            IsValid = false,
            AcquisitionModeName = acquisitionName,
            IsSingleFrame = singleFrame,
            StatusMessage = message
        };
}
