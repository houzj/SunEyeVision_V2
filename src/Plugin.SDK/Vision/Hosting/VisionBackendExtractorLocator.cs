using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Vision.Hosting
{
    /// <summary>
    /// 跨后端提取委托：从任意 <see cref="IVisionImage"/> 提取其底层引擎对象。
    /// 返回 null 表示该实现不支持/无法提取。SDK 仅定义委托契约（object 承载引擎对象），
    /// 不引用任何具体引擎类型——HalAdapter/CvAdapter 各自在装配时把实现注册进来，
    /// 从而在不互引的前提下完成 Cv↔Hal 交叉转换（对称架构 D4）。
    /// </summary>
    public delegate object? VisionImageBackendExtractor(IVisionImage image);

    /// <summary>
    /// 跨后端提取定位器：主程序装配时注入两个方向的具体实现闭包。
    /// ToHalcon：提取 halcon HImage（供 HalAdapter 在收到非 Hal 图像时转成 HImage）；
    /// ToOpenCv：提取 OpenCvSharp Mat（供 CvAdapter 在收到非 Cv 图像时转成 CvImage）。
    /// </summary>
    public static class VisionBackendExtractorLocator
    {
        /// <summary>提取底层 halcon HImage 的委托（返回 object 承载 HImage；null=不支持）。</summary>
        public static VisionImageBackendExtractor? ToHalcon { get; set; }

        /// <summary>提取底层 OpenCvSharp Mat 的委托（返回 object 承载 Mat；null=不支持）。</summary>
        public static VisionImageBackendExtractor? ToOpenCv { get; set; }
    }
}
