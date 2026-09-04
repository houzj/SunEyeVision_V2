using System;
using System.Windows.Media.Imaging;
using SunEyeVision.CvAdapter;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;

namespace SunEyeVision.UI.Converters
{
    /// <summary>
    /// IVisionImage → WPF BitmapSource 的统一转换入口（UI 适配层）。
    /// 对称架构：图像显示由适配层具体类型负责（HalAdapter.HalImage / CvAdapter.CvVisionImageAdapter 各自实现 ToBitmapSource），
    /// 此处仅做类型分派，确保 SDK 契约接口本身不含 WPF 转换职责。
    /// </summary>
    public static class VisionImageToBitmapSourceConverter
    {
        public static BitmapSource? ToBitmapSource(IVisionImage? image)
        {
            if (image == null || image.IsEmpty)
                return null;

            return image switch
            {
                HalImage hal => hal.ToBitmapSource(),
                CvVisionImageAdapter cv => cv.ToBitmapSource(),
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
