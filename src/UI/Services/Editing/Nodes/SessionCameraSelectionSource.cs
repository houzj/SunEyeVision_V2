using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>
/// 把方案中的相机设备、连接状态与打开命令暴露给参数页的相机行。
/// 参数页只依赖 ICameraSelectionSource，不直接访问设备驱动。
/// </summary>
internal sealed class SessionCameraSelectionSource(
    Func<IReadOnlyList<CameraChoiceItem>> choices,
    Func<string, string?>? status,
    Action<string?>? open) : ICameraSelectionSource
{
    public IReadOnlyList<CameraChoiceItem> Choices => choices();

    /// <summary>设备列表由参数行在每次展开时重新读取，因此不需要主动推送变更。</summary>
    public event Action? Changed
    {
        add { }
        remove { }
    }

    public string Status(string? deviceId) => status?.Invoke(deviceId ?? string.Empty) ?? string.Empty;

    public void Open(string? deviceId) => open?.Invoke(deviceId);
}
