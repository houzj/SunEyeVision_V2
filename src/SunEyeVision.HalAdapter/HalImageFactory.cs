using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Vision.Hosting;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// Halcon 侧图像加载工厂：实现 SDK <see cref="IVisionImageFactory"/>，
    /// 由主程序装配注册到 <see cref="VisionImageFactoryLocator"/>（对称架构 S5）。
    /// </summary>
    public sealed class HalImageFactory : IVisionImageFactory
    {
        public bool CanCreateFromFile(string path) => true;

        public IVisionImage CreateFromFile(string path) => HalImage.CreateFromFile(path);
    }
}
