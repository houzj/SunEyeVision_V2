namespace SunEyeVision.Plugin.Editing.Wpf.Models.Imaging
{
    /// <summary>
    /// 图像加载状态
    /// </summary>
    /// <remarks>
    /// 表示特定格式图像的加载状态，用于实现延迟加载和状态追踪。
    /// </remarks>
    public enum ImageLoadState
    {
        /// <summary>
        /// 未加载 - 数据尚未加载
        /// </summary>
        NotLoaded,

        /// <summary>
        /// 加载中 - 正在异步加载
        /// </summary>
        Loading,

        /// <summary>
        /// 已加载 - 数据已加载并可用
        /// </summary>
        Loaded,

        /// <summary>
        /// 加载失败 - 加载过程中发生错误
        /// </summary>
        Failed,

        /// <summary>
        /// 已释放 - 数据已被释放
        /// </summary>
        Released
    }
}
