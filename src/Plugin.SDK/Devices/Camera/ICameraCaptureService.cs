using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.Plugin.SDK.Devices.Camera;

/// <summary>
/// 宿主提供给工作流工具和插件的相机采集能力。
/// </summary>
public interface ICameraCaptureService
{
    Task<CameraFrame> CaptureAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken);
}
