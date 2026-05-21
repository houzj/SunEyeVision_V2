using System;
using HalconDotNet;
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
            hImage = null;
            return false;
        }

        public static double GetDouble(this HTuple tuple) => tuple.D;
        public static long GetLong(this HTuple tuple) => tuple.L;
    }
}