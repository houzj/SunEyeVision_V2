using System;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Vision.Hosting
{
    /// <summary>
    /// 图像创建门面（宿主侧统一入口）：仅保留经 <see cref="VisionImageFactoryLocator"/>
    /// 注册的工厂分发，SDK 不携带任何引擎实现（对称架构）。
    /// 引擎创建方法（FromBytes/FromBitmapSource/FromMat）属适配层职责，
    /// 由 HalAdapter/CvAdapter 的静态工厂（HalImage/CvImage 等）提供，此处不重复。
    /// </summary>
    public static class VisionImage
    {
        /// <summary>
        /// 从文件加载图像：经注册工厂分发。未注册可处理该路径的工厂时返回 null
        /// （由调用方判断并给出明确错误）。
        /// </summary>
        public static IVisionImage? FromFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            var factory = VisionImageFactoryLocator.Current;
            if (factory != null && factory.CanCreateFromFile(path))
                return factory.CreateFromFile(path);
            return null;
        }
    }
}
