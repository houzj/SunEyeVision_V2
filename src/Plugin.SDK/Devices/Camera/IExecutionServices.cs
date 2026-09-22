namespace SunEyeVision.Plugin.SDK.Devices.Camera;

/// <summary>
/// 受控的执行期服务访问入口，不暴露宿主 DI 容器或 UI 对象。
/// </summary>
public interface IExecutionServices
{
    T? Get<T>() where T : class;
}
