using System;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using SunEyeVision.CvAdapter;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.UI.Controls;

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

            var frame = ImageFrameProvenance.CaptureInput(image);
            var bitmap = image switch
            {
                HalImage hal => hal.ToBitmapSource(),
                CvVisionImageAdapter cv => cv.ToBitmapSource(),
                _ => CreateBitmapSource(image),
            };
            // Do not publish a bitmap labelled with an identity that changed during conversion.
            if (frame != ImageFrameProvenance.CaptureInput(image)) return null;
            PreviewFrameProvenance.Attach(bitmap, frame);
            return bitmap;
        }

        private static BitmapSource CreateBitmapSource(IVisionImage image)
        {
            // 相机适配器只需要实现 SDK 的像素数据契约即可接入预览，
            // 不要求每个厂商适配器再依赖 WPF 或重复编写显示转换。
            var pixels = image.GetPixelData();
            var format = image.Channels == 1 ? PixelFormats.Gray8 : PixelFormats.Bgr24;
            var bytesPerPixel = format.BitsPerPixel / 8;
            var stride = image.Width * bytesPerPixel;
            var requiredLength = checked(stride * image.Height);
            if (pixels.Length < requiredLength)
            {
                Array.Resize(ref pixels, requiredLength);
            }

            var bitmap = BitmapSource.Create(
                image.Width,
                image.Height,
                96,
                96,
                format,
                null,
                pixels,
                stride);
            bitmap.Freeze();
            return bitmap;
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
