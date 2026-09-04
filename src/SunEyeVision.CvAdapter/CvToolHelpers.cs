using System;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;
using CvImage = SunEyeVision.CvEngine.CvImage;

namespace SunEyeVision.CvAdapter
{
    /// <summary>
    /// Cv 工具取图助手（适配层扩展）：从 <see cref="ToolParameters.ResolvedInputImage"/> 解析出
    /// CvEngine 纯图像 CvImage。CvAdapter 不引 halcon/HalAdapter（对称架构 D4），
    /// 若输入是 CvVisionImageAdapter 直接解包；其它后端（如 Hal）经装配注册的
    /// <see cref="VisionBackendExtractorLocator.ToOpenCv"/> 提取 Mat 再包装。
    /// </summary>
    public static class CvToolHelpers
    {
        public static CvImage GetCvImage(this ToolParameters parameters)
        {
            var image = parameters.ResolvedInputImage;
            if (image is CvVisionImageAdapter cvAdapter && !cvAdapter.IsEmpty)
                return cvAdapter.Unwrap();

            // 跨后端转换：经装配注册的提取器把非 Cv 图像（如 HalAdapter 产物）转成 OpenCv Mat。
            // Mat 生命周期由注册的提取器约定（S5 装配由转换实现方负责所有权），此处轻包装不 Clone。
            if (image != null)
            {
                var extractor = VisionBackendExtractorLocator.ToOpenCv;
                if (extractor != null && extractor(image) is Mat mat && !mat.Empty())
                    return new CvImage(mat);
            }

            throw new InvalidOperationException("Input is not CvImage, type: " + (image?.GetType().Name ?? "null"));
        }

        public static bool TryGetCvImage(this ToolParameters parameters, out CvImage? cvImage)
        {
            var image = parameters.ResolvedInputImage;
            if (image is CvVisionImageAdapter cvAdapter && !cvAdapter.IsEmpty)
            {
                cvImage = cvAdapter.Unwrap();
                return true;
            }

            if (image != null)
            {
                var extractor = VisionBackendExtractorLocator.ToOpenCv;
                if (extractor != null && extractor(image) is Mat mat && !mat.Empty())
                {
                    cvImage = new CvImage(mat);
                    return true;
                }
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
