using System;
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
    }
}
