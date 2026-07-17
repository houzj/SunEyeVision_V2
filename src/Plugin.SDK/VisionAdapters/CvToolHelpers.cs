using System;
using System.Runtime.InteropServices;
using HalconDotNet;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public static class CvToolHelpers
    {
        public static CvImage GetCvImage(this ToolParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            if (image is CvImage cvImage)
            {
                if (!cvImage.Empty)
                    return cvImage;
                throw new InvalidOperationException("Input CvImage is empty");
            }

            // 跨后端转换：HalImage → CvImage
            if (image is HalImage halImage)
                return ConvertHalImageToCvImage(halImage);

            throw new InvalidOperationException("Input is not CvImage, type: " + (image?.GetType().Name ?? "null"));
        }

        public static bool TryGetCvImage(this ToolParameters parameters, out CvImage? cvImage)
        {
            var image = parameters.ResolvedInputImage;
            if (image is CvImage cvImg && !cvImg.Empty)
            {
                cvImage = cvImg;
                return true;
            }

            // 跨后端转换：HalImage → CvImage
            if (image is HalImage halImage)
            {
                cvImage = ConvertHalImageToCvImage(halImage);
                return !cvImage.Empty;
            }

            cvImage = null;
            return false;
        }

        public static Mat GetCvMat(this ToolParameters parameters)
        {
            var cvImage = parameters.GetCvImage();
            var mat = cvImage.Image;
            if (mat != null && !mat.Empty())
                return mat;
            throw new InvalidOperationException("CvImage Mat data is empty or null");
        }

        public static bool TryGetCvMat(this ToolParameters parameters, out Mat? mat)
        {
            if (parameters.TryGetCvImage(out var cvImage))
            {
                mat = cvImage!.Image;
                return true;
            }
            mat = null;
            return false;
        }

        /// <summary>
        /// 将 HalImage 转换为 CvImage（跨后端转换）
        /// </summary>
        private static CvImage ConvertHalImageToCvImage(HalImage halImage)
        {
            var hImage = halImage.Unwrap();
            if (hImage == null || !hImage.IsInitialized())
                throw new InvalidOperationException("Source HalImage is not initialized");

            HOperatorSet.GetImageSize(hImage, out var width, out var height);
            HOperatorSet.CountChannels(hImage, out var channels);

            var w = width.I;
            var h = height.I;
            var ch = channels.I;

            if (ch == 1)
            {
                // 单通道灰度图：直接复制像素数据
                HOperatorSet.GetImagePointer1(hImage, out var ptr, out var typeStr, out var _, out var _);
                byte[] pixels = new byte[w * h];
                Marshal.Copy(ptr, pixels, 0, pixels.Length);
                var mat = new Mat(h, w, MatType.CV_8UC1);
                Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
                return new CvImage(mat);
            }
            else if (ch == 3)
            {
                // 三通道彩色图：Halcon 平面 RGB → OpenCV 交错 BGR
                HOperatorSet.GetImagePointer3(hImage, out var rPtr, out var gPtr, out var bPtr, out var typeStr, out var _, out var _);
                byte[] rPixels = new byte[w * h];
                byte[] gPixels = new byte[w * h];
                byte[] bPixels = new byte[w * h];
                Marshal.Copy(rPtr, rPixels, 0, rPixels.Length);
                Marshal.Copy(gPtr, gPixels, 0, gPixels.Length);
                Marshal.Copy(bPtr, bPixels, 0, bPixels.Length);

                // 交错排列为 BGR（OpenCV 格式）
                byte[] bgr = new byte[w * h * 3];
                for (int i = 0; i < w * h; i++)
                {
                    bgr[i * 3]     = bPixels[i];  // B
                    bgr[i * 3 + 1] = gPixels[i];  // G
                    bgr[i * 3 + 2] = rPixels[i];  // R
                }

                var mat = new Mat(h, w, MatType.CV_8UC3);
                Marshal.Copy(bgr, 0, mat.Data, bgr.Length);
                return new CvImage(mat);
            }

            throw new NotSupportedException($"不支持的通道数: {ch}，HalImage → CvImage 转换仅支持 1/3 通道");
        }
    }
}
