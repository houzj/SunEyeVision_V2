namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    /// <summary>
    /// 图像创建工厂契约：算法库/适配层实现具体图像加载（HalImage/CvImage 等），
    /// 框架层只消费 IVisionImage，避免 SDK 反向依赖引擎。
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
    /// SDK 门面 <see cref="VisionImage"/> 经它分发图像创建请求。
    /// 未注入时门面回退到 SDK 内默认加载路径（过渡期行为，S1 保持兼容）。
    /// </summary>
    public static class VisionImageFactoryLocator
    {
        public static IVisionImageFactory? Current { get; set; }
    }
}
