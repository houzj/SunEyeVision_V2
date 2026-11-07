using System;

namespace SunEyeVision.Plugin.Editing.Wpf.Models.Imaging
{
    /// <summary>
    /// 图像格式标志
    /// </summary>
    /// <remarks>
    /// 定义不同用途的图像格式，支持按需加载和预加载策略。
    /// 
    /// 使用场景：
    /// - Thumbnail: 缩略图，用于图像预览器、快速浏览
    /// - FullImage: 完整显示图像(BitmapSource)，用于主显示区域
    /// - Mat: OpenCV处理格式，用于算法处理节点
    /// </remarks>
    [Flags]
    public enum ImageFormat
    {
        /// <summary>
        /// 无格式
        /// </summary>
        None = 0,

        /// <summary>
        /// 缩略图格式 - 用于预览器、快速浏览
        /// 通常尺寸较小(如60x60)，内存占用低
        /// </summary>
        Thumbnail = 1 << 0,

        /// <summary>
        /// 完整显示图像(BitmapSource) - 用于主显示区域
        /// 适合WPF显示的格式，可能经过缩放以适应屏幕
        /// </summary>
        FullImage = 1 << 1,

        /// <summary>
        /// OpenCV Mat格式 - 用于算法处理
        /// 原始像素数据，用于图像处理算法
        /// </summary>
        Mat = 1 << 2,

        /// <summary>
        /// 所有格式
        /// </summary>
        All = Thumbnail | FullImage | Mat
    }
}
