using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Vision.Hosting
{
    /// <summary>
    /// 图像显示桥接：将 IVisionImage 转换为 WPF BitmapSource。
    /// 转换逻辑实现在 UI 适配层（避免 SDK 接口承担 WPF 转换细节），
    /// 通过 VisionImageConverterLocator 在 UI 启动时注入。
    /// </summary>
    public interface IVisionImageDisplayBridge
    {
        BitmapSource? ToBitmapSource(IVisionImage? image);
    }

    /// <summary>
    /// 静态定位器：UI 启动时设置 Current，SDK 层通过它完成图像→BitmapSource 转换。
    /// SDK 层不反向依赖 UI，仅依赖此最小桥接口。
    /// </summary>
    public static class VisionImageConverterLocator
    {
        public static IVisionImageDisplayBridge? Current { get; set; }
    }
}
