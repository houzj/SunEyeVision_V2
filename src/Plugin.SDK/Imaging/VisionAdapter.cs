using System;

namespace SunEyeVision.Plugin.SDK.Imaging
{
    /// <summary>
    /// 视觉适配器注册点 - 应用启动时设置具体工厂实现
    /// </summary>
    /// <remarks>
    /// 使用示例（App.xaml.cs 或 Program.cs）：
    /// <code>
    /// // 使用 CvEngine 内建适配
    /// VisionAdapter.Factory = new CvEngineVisionFactory();
    /// 
    /// // 使用 Halcon 适配
    /// VisionAdapter.Factory = new HalconVisionFactory();
    /// </code>
    /// 
    /// 未初始化时访问 <see cref="Factory"/> 会抛出 <see cref="InvalidOperationException"/>
    /// </remarks>
    public static class VisionAdapter
    {
        private static IVisionFactory? _factory;

        /// <summary>
        /// 获取或设置当前使用的 IVisionFactory 实例
        /// </summary>
        /// <exception cref="InvalidOperationException">尝试获取未初始化的工厂</exception>
        public static IVisionFactory Factory
        {
            get => _factory ?? throw new InvalidOperationException(
                "VisionAdapter 未初始化。请在应用启动时设置 VisionAdapter.Factory。");
            set => _factory = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>判断是否已初始化</summary>
        public static bool IsInitialized => _factory != null;

        /// <summary>重置适配器（用于测试）</summary>
        public static void Reset() => _factory = null;
    }
}
