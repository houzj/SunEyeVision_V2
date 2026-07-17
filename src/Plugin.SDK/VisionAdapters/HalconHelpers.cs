using System;
using System.Runtime.InteropServices;
using HalconDotNet;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public static class HalconToolHelpers
    {
        public static HImage GetHalconImage(this ToolParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            if (image is HalImage halImage)
            {
                var hImage = halImage.Unwrap();
                if (hImage != null && hImage.IsInitialized())
                    return hImage;
                throw new InvalidOperationException("Input HImage is not initialized");
            }

            // 跨后端转换：CvImage → HalImage
            if (image is CvImage cvImage)
                return ConvertCvImageToHalImage(cvImage);

            throw new InvalidOperationException("Input is not Halcon HImage, type: " + (image?.GetType().Name ?? "null"));
        }

        public static bool TryGetHalconImage(this ToolParameters parameters, out HImage? hImage)
        {
            var image = parameters.ResolvedInputImage;
            if (image is HalImage halImage)
            {
                var unwrapped = halImage.Unwrap();
                if (unwrapped != null && unwrapped.IsInitialized())
                {
                    hImage = unwrapped;
                    return true;
                }
            }

            // 跨后端转换：CvImage → HalImage
            if (image is CvImage cvImage && !cvImage.Empty)
            {
                hImage = ConvertCvImageToHalImage(cvImage);
                return hImage != null && hImage.IsInitialized();
            }

            hImage = null;
            return false;
        }

        public static double GetDouble(this HTuple tuple) => tuple.D;
        public static long GetLong(this HTuple tuple) => tuple.L;

        /// <summary>
        /// 将 CvImage 转换为 Halcon HImage（跨后端转换）
        /// </summary>
        private static HImage ConvertCvImageToHalImage(CvImage cvImage)
        {
            if (cvImage.Empty)
                throw new InvalidOperationException("Source CvImage is empty");

            var mat = cvImage.Image;
            if (mat == null || mat.Empty())
                throw new InvalidOperationException("CvImage Mat data is empty");

            int w = mat.Width;
            int h = mat.Height;
            int ch = mat.Channels();

            if (ch == 1)
            {
                byte[] pixels = new byte[w * h];
                Marshal.Copy(mat.Data, pixels, 0, pixels.Length);
                var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                try
                {
                    HOperatorSet.GenImage1(out var hObj, "byte", w, h, handle.AddrOfPinnedObject());
                    return new HImage(hObj);
                }
                finally
                {
                    handle.Free();
                }
            }
            else if (ch == 3)
            {
                // OpenCV 交错 BGR → Halcon 平面 RGB
                byte[] bgr = new byte[w * h * 3];
                Marshal.Copy(mat.Data, bgr, 0, bgr.Length);

                byte[] rPixels = new byte[w * h];
                byte[] gPixels = new byte[w * h];
                byte[] bPixels = new byte[w * h];
                for (int i = 0; i < w * h; i++)
                {
                    bPixels[i] = bgr[i * 3];      // B
                    gPixels[i] = bgr[i * 3 + 1];  // G
                    rPixels[i] = bgr[i * 3 + 2];  // R
                }

                GCHandle rHandle = GCHandle.Alloc(rPixels, GCHandleType.Pinned);
                GCHandle gHandle = GCHandle.Alloc(gPixels, GCHandleType.Pinned);
                GCHandle bHandle = GCHandle.Alloc(bPixels, GCHandleType.Pinned);
                try
                {
                    HOperatorSet.GenImage3(out var hObj, "byte", w, h,
                        rHandle.AddrOfPinnedObject(),
                        gHandle.AddrOfPinnedObject(),
                        bHandle.AddrOfPinnedObject());
                    return new HImage(hObj);
                }
                finally
                {
                    bHandle.Free();
                    gHandle.Free();
                    rHandle.Free();
                }
            }

            throw new NotSupportedException($"不支持的通道数: {ch}，CvImage → HalImage 转换仅支持 1/3 通道");
        }
    }
}