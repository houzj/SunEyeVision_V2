using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Vision;

namespace SunEyeVision.Plugin.Runtime.Vision
{
    /// <summary>Optional host capability for an owned, exact pixel/Domain copy of a
    /// neutral image. Algorithm plugins do not depend on the selected backend.</summary>
    public interface IReadOnlyImageViewFactory
    {
        IVisionImage CopyImage(IReadOnlyImage source);
    }

    /// <summary>
    /// 图像创建工厂契约：算法库/适配层实现具体图像加载（HalImage/CvImage 等），
    /// 框架层只消费 IVisionImage，避免运行时层反向依赖引擎。
    /// 实现方注册到 <see cref="VisionImageFactoryLocator"/>，主程序启动时注入。
    /// </summary>
    public interface IVisionImageFactory
    {
        /// <summary>该实现是否能加载指定路径图像。</summary>
        bool CanCreateFromFile(string path);

        /// <summary>从文件加载图像。</summary>
        IVisionImage CreateFromFile(string path);
    }

    /// <summary>
    /// 静态定位器：主程序启动时设置 Current（仿 VisionImageConverterLocator 现有模式），
    /// 门面 <see cref="VisionImage"/> 经它分发图像创建请求。
    /// </summary>
    public static class VisionImageFactoryLocator
    {
        public static IVisionImageFactory? Current { get; set; }
    }
}
