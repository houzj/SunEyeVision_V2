using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;

namespace SunEyeVision.CvAdapter
{
    /// <summary>
    /// Cv 侧图像加载工厂：实现 SDK <see cref="IVisionImageFactory"/>，
    /// 由主程序装配注册到 <see cref="VisionImageFactoryLocator"/>（对称架构 S5）。
    /// </summary>
    public sealed class CvImageFactory : IVisionImageFactory
    {
        public bool CanCreateFromFile(string path) => true;

        public IVisionImage CreateFromFile(string path)
        {
            var mat = new Mat(path, ImreadModes.Color);
            return new CvVisionImageAdapter(CvImage.FromMat(mat));
        }
    }

    /// <summary>
    /// CvAdapter 装配启动入口：注册 Cv 侧图像类型到 SDK 词汇表（如 typeof(Mat)→"Image"），
    /// 由主程序装配调用（对称架构 S5 / D2 前提）。
    /// </summary>
    public static class CvAdapterStartup
    {
        /// <summary>把 OpenCvSharp.Mat 注册到契约词目 "Image"（恢复 Mat→Image 词汇映射）。</summary>
        public static void RegisterSdkVocabulary()
        {
            SdkTypeVocabulary.RegisterType(typeof(Mat), "Image");
        }
    }
}
