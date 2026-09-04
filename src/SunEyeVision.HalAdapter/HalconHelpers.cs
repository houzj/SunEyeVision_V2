using System;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.HalAdapter
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

            // 跨后端转换：经装配注册的提取器把非 Hal 图像（如 CvAdapter 产物）转成 HImage。
            // HalAdapter 不引 CvEngine（对称架构 D4），具体像素转换由主程序装配注册。
            if (image != null)
            {
                var extractor = VisionBackendExtractorLocator.ToHalcon;
                if (extractor != null && extractor(image) is HImage converted)
                    return converted;
            }

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

            // 跨后端转换：经装配注册的提取器把非 Hal 图像转成 HImage。
            if (image != null)
            {
                var extractor = VisionBackendExtractorLocator.ToHalcon;
                if (extractor != null && extractor(image) is HImage converted)
                {
                    hImage = converted;
                    return hImage.IsInitialized();
                }
            }

            hImage = null;
            return false;
        }

        public static double GetDouble(this HTuple tuple) => tuple.D;
        public static long GetLong(this HTuple tuple) => tuple.L;
    }
}
