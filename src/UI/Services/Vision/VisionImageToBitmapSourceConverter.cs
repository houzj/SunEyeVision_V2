using System;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.UI.Services.Vision
{
    /// <summary>
    /// IVisionImage → WPF BitmapSource 的统一转换入口（UI 适配层）。
    /// 实际转换逻辑由 SDK 具体类（CvImage / HalImage）实现（Halcon / OpenCvSharp 转换在其中），
    /// 此处仅做类型分派，确保 IVisionImage 接口本身不含 WPF 转换职责（去耦合目标）。
    /// </summary>
    public static class VisionImageToBitmapSourceConverter
    {
        public static BitmapSource? ToBitmapSource(IVisionImage? image)
        {
            if (image == null || image.IsEmpty)
                return null;

            return image switch
            {
                CvImage cv => cv.ToBitmapSource(),
                HalImage hal => hal.ToBitmapSource(),
                _ => throw new NotSupportedException(
                    $"不支持的图像类型: {image.GetType().FullName}"),
            };
        }
    }

    /// <summary>
    /// IVisionImageDisplayBridge 的 UI 实现，委托给 VisionImageToBitmapSourceConverter。
    /// 在 App 启动时注册到 VisionImageConverterLocator.Current。
    /// </summary>
    public sealed class UiVisionImageToBitmapSourceConverter : IVisionImageDisplayBridge
    {
        public BitmapSource? ToBitmapSource(IVisionImage? image)
            => VisionImageToBitmapSourceConverter.ToBitmapSource(image);
    }
}
