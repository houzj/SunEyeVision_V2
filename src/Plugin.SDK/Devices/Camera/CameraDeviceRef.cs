namespace SunEyeVision.Plugin.SDK.Devices.Camera;

/// <summary>
/// 工作流和插件使用的逻辑相机引用。
/// 不保存 IP、SDK 句柄或厂商类型；物理设备解析由宿主运行时完成。
/// </summary>
public sealed record CameraDeviceRef(string Value)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

    public override string ToString() => Value;
}
