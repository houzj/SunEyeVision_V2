namespace SunEyeVision.Plugin.Contracts;

/// <summary>可选的相机设备：显示名与稳定标识。</summary>
public sealed record CameraChoiceItem(string Name, string Id);

/// <summary>
/// 相机选择所需的宿主能力。平台参数页只依赖本契约，
/// 由主程序的相机管理实现并注入，避免配置层反向依赖设备驱动。
/// </summary>
public interface ICameraSelectionSource
{
    /// <summary>当前可用设备；无可用设备时返回空集合。</summary>
    IReadOnlyList<CameraChoiceItem> Choices { get; }

    /// <summary>设备列表或连接状态变化时触发。</summary>
    event Action? Changed;

    /// <summary>给定设备标识的当前状态描述，供参数行显示。</summary>
    string Status(string? deviceId);

    /// <summary>请求打开或关闭设备；标识为空表示关闭。</summary>
    void Open(string? deviceId);
}
