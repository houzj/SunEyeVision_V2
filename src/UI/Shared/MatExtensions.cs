using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace SunEyeVision.UI.Shared
{
    /// <summary>
    /// 图像扩展方法
    /// </summary>
    public static class ImageExtensions
    {
        /// <summary>
        /// 将 WPF BitmapSource 转换为 OpenCvSharp.Mat
        /// </summary>
        /// <param name="bitmapSource">WPF BitmapSource 对象</param>
        /// <returns>OpenCV Mat 对象</returns>
        public static Mat? ToMat(this BitmapSource bitmapSource)
        {
            if (bitmapSource == null)
                return null;

            try
            {
                // 尝试使用 OpenCvSharp4.WpfExtensions 的扩展方法（如果可用）
                // 如果不可用，使用备用方案
                try
                {
                    var matType = GetMatTypeFromPixelFormat(bitmapSource.Format);
                    var width = bitmapSource.PixelWidth;
                    var height = bitmapSource.PixelHeight;

                    var mat = new Mat(height, width, matType);

                    // 复制像素数据
                    var stride = width * ((bitmapSource.Format.BitsPerPixel + 7) / 8);
                    var pixels = new byte[height * stride];
                    bitmapSource.CopyPixels(pixels, stride, 0);

                    // 将像素数据复制到 Mat
                    System.Runtime.InteropServices.Marshal.Copy(pixels, 0, mat.Data, pixels.Length);

                    return mat;
                }
                catch
                {
                    // 备用方案：通过内存流和 Bitmap 转换
                    return ConvertViaMemoryStream(bitmapSource);
                }
            }
            catch (Exception ex)
            {
return null;
            }
        }

        /// <summary>
        /// 获取对应像素格式的 MatType
        /// </summary>
        private static MatType GetMatTypeFromPixelFormat(System.Windows.Media.PixelFormat? format)
        {
            if (format == null)
                return MatType.CV_8UC3;

            if (format == PixelFormats.Bgr24)
                return MatType.CV_8UC3;
            if (format == PixelFormats.Bgra32)
                return MatType.CV_8UC4;
            if (format == PixelFormats.Gray8)
                return MatType.CV_8UC1;
            if (format == PixelFormats.Rgb24)
                return MatType.CV_8UC3;

            // 默认返回 BGR 格式
            return MatType.CV_8UC3;
        }

        /// <summary>
        /// 通过内存流方式转换 BitmapSource 到 Mat
        /// </summary>
        private static Mat? ConvertViaMemoryStream(BitmapSource bitmapSource)
        {
            try
            {
                // 编码为 PNG 格式
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));

                using var stream = new MemoryStream();
                encoder.Save(stream);

                // 使用 OpenCvSharp 从内存流加载图像
                var mat = Mat.FromImageData(stream.ToArray(), ImreadModes.Color);
                return mat;
            }
            catch (Exception ex)
            {
return null;
            }
        }
    }

    /// <summary>
    /// Mat 图像扩展方法
    /// </summary>
    public static class MatExtensions
    {
        /// <summary>
        /// 将 OpenCvSharp.Mat 转换为 WPF BitmapSource
        /// </summary>
        /// <param name="mat">OpenCV Mat 对象</param>
        /// <returns>WPF BitmapSource 对象</returns>
        public static BitmapSource? ToBitmapSource(this Mat mat)
        {
            if (mat == null || mat.Empty())
                return null;

            try
            {
                // 使用 OpenCvSharp.Extensions.BitmapConverter 的 ToBitmap 方法
                using var bitmap = mat.ToBitmap();
                if (bitmap == null)
                    return null;

                // 将 Bitmap 转换为 BitmapSource
                var hBitmap = bitmap.GetHbitmap();
                try
                {
                    var bitmapSource = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                        hBitmap,
                        IntPtr.Zero,
                        System.Windows.Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    bitmapSource.Freeze();
                    return bitmapSource;
                }
                finally
                {
                    // 释放 HBitmap 资源
                    DeleteObject(hBitmap);
                }
            }
            catch (Exception ex)
            {
// 备用方案：通过内存流转换
                try
                {
                    return ConvertViaMemoryStream(mat);
                }
                catch
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// 通过内存流方式转换 Mat 到 BitmapSource
        /// </summary>
        private static BitmapSource? ConvertViaMemoryStream(Mat mat)
        {
            // 确保图像格式正确
            Mat? convertedMat = null;
            Mat sourceMat = mat;

            // 如果不是标准格式，转换为 BGR
            if (mat.Type() != MatType.CV_8UC3 && mat.Type() != MatType.CV_8UC1 && mat.Type() != MatType.CV_8UC4)
            {
                convertedMat = new Mat();
                Cv2.CvtColor(mat, convertedMat, ColorConversionCodes.GRAY2BGR);
                sourceMat = convertedMat;
            }

            // 编码为 PNG 格式
            Cv2.ImEncode(".png", sourceMat, out byte[] bytes);

            convertedMat?.Dispose();

            // 从字节数组创建 BitmapSource
            using var stream = new MemoryStream(bytes);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var bitmapSource = decoder.Frames[0];
            bitmapSource.Freeze();
            return bitmapSource;
        }

        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);
    }
}
