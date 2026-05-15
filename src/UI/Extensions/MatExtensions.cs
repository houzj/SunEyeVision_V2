using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;

namespace SunEyeVision.UI.Extensions
{
    /// <summary>
    /// Halcon 图像扩展方法
    /// </summary>
    public static class HImageExtensions
    {
        /// <summary>
        /// 将 Halcon HImage 转换为 WPF BitmapSource
        /// </summary>
        public static BitmapSource? ToBitmapSource(this HImage image)
        {
            if (image == null || !image.IsInitialized())
                return null;

            try
            {
                HTuple width, height, channels, type;
                HOperatorSet.GetImageSize(image, out width, out height);
                HOperatorSet.CountChannels(image, out channels);
                HOperatorSet.GetImageType(image, out type);

                int w = width.I, h = height.I;

                if (channels.I == 1 && type.S == "byte")
                {
                    HTuple pointer, imgType;
                    HOperatorSet.GetImagePointer1(image, out pointer, out imgType, out width, out height);
                    var bs = BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, pointer, h * w, w);
                    bs.Freeze();
                    return bs;
                }
                else if (channels.I == 3 && type.S == "byte")
                {
                    // For RGB, convert via memory
                    HOperatorSet.GetImagePointer3(image, out var ptrR, out var ptrG, out var ptrB, out var imgType,
                        out width, out height);
                    int stride = w * 3;
                    byte[] pixels = new byte[h * stride];

                    unsafe
                    {
                        byte* rPtr = (byte*)ptrR.IP;
                        byte* gPtr = (byte*)ptrG.IP;
                        byte* bPtr = (byte*)ptrB.IP;
                        fixed (byte* dst = pixels)
                        {
                            for (int y = 0; y < h; y++)
                            {
                                for (int x = 0; x < w; x++)
                                {
                                    int idx = y * stride + x * 3;
                                    dst[idx] = rPtr[y * w + x];     // R
                                    dst[idx + 1] = gPtr[y * w + x]; // G
                                    dst[idx + 2] = bPtr[y * w + x]; // B
                                }
                            }
                        }
                    }

                    var bs = BitmapSource.Create(w, h, 96, 96, PixelFormats.Rgb24, null,
                        pixels, stride);
                    bs.Freeze();
                    return bs;
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 读取图像文件为 HImage
        /// </summary>
        public static HImage? ReadImageFile(string filePath)
        {
            try
            {
                HOperatorSet.ReadImage(out var hObj, filePath);
                return new HImage(hObj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 缩放 HImage
        /// </summary>
        public static HImage? ZoomToSize(this HImage image, int width, int height, string interpolation = "constant")
        {
            if (image == null || !image.IsInitialized())
                return null;
            try
            {
                HOperatorSet.ZoomImageSize(image, out var zoomedHObj, width, height, interpolation);
                return new HImage(zoomedHObj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 缩小图像以适应指定尺寸（保持比例）
        /// </summary>
        public static HImage? ZoomToFit(this HImage image, int maxSize)
        {
            if (image == null || !image.IsInitialized())
                return null;
            try
            {
                HTuple w, h;
                HOperatorSet.GetImageSize(image, out w, out h);
                int maxDim = Math.Max(w.I, h.I);
                if (maxDim <= maxSize) return image.Clone();
                double scale = (double)maxSize / maxDim;
                int newW = (int)(w.I * scale);
                int newH = (int)(h.I * scale);
                HOperatorSet.ZoomImageSize(image, out var zoomedHObj, newW, newH, "constant");
                return new HImage(zoomedHObj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取 HImage 的元数据
        /// </summary>
        public static (int Width, int Height, int Channels, string Type) GetInfo(this HImage image)
        {
            HOperatorSet.GetImageSize(image, out var w, out var h);
            HOperatorSet.CountChannels(image, out var c);
            HOperatorSet.GetImageType(image, out var t);
            return (w.I, h.I, c.I, t.S);
        }
    }
}
