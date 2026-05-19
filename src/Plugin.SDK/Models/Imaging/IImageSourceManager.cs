using System;
using SunEyeVision.Plugin.SDK.Imaging;

namespace SunEyeVision.Plugin.SDK.Models.Imaging
{
    /// <summary>
    /// 图像源管理器接口
    /// </summary>
    /// <remarks>
    /// <para>
    /// IImageSourceManager负责创建、缓存和管理IImageSource实例。
    /// 核心职责：
    /// </para>
    /// <list type="bullet">
    /// <item><description>创建图像源：从文件、内存等来源创建IImageSource</description></item>
    /// <item><description>缓存管理：实现LRU策略，自动释放不常用的图像源</description></item>
    /// <item><description>内存监控：响应系统内存压力，主动释放资源</description></item>
    /// <item><description>预加载调度：批量预加载提升用户体验</description></item>
    /// </list>
    /// </remarks>
    public interface IImageSourceManager : IDisposable
    {
        /// <summary>
        /// 创建文件图像源
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>图像源实例</returns>
        IImageSource CreateFromFile(string filePath);

        /// <summary>
        /// 创建内存图像源（用于算法处理结果）
        /// </summary>
        /// <param name="image">IVisionImage 图像（所有权转移，管理器负责释放）</param>
        /// <param name="sourceId">可选的图像源ID，不提供则自动生成</param>
        /// <returns>图像源实例</returns>
        IImageSource CreateFromMemory(IVisionImage image, string? sourceId = null);

        /// <summary>
        /// 从现有BitmapSource创建内存图像源
        /// </summary>
        /// <param name="bitmapSource">BitmapSource对象</param>
        /// <param name="sourceId">可选的图像源ID</param>
        /// <returns>图像源实例</returns>
        IImageSource CreateFromBitmapSource(System.Windows.Media.Imaging.BitmapSource bitmapSource, string? sourceId = null);

        /// <summary>
        /// 获取或创建图像源
        /// </summary>
        /// <param name="imageSourceId">图像源ID</param>
        /// <returns>图像源实例，不存在返回null</returns>
        IImageSource? GetImageSource(string imageSourceId);

        /// <summary>
        /// 检查图像源是否存在
        /// </summary>
        /// <param name="imageSourceId">图像源ID</param>
        /// <returns>是否存在</returns>
        bool Contains(string imageSourceId);

        /// <summary>
        /// 移除图像源
        /// </summary>
        /// <param name="imageSourceId">图像源ID</param>
        /// <returns>是否成功移除</returns>
        bool Remove(string imageSourceId);

        /// <summary>
        /// 清空所有图像源
        /// </summary>
        void Clear();

        /// <summary>
        /// 预加载图像源列表
        /// </summary>
        /// <param name="imageSourceIds">图像源ID列表</param>
        /// <param name="formats">要预加载的格式</param>
        /// <remarks>
        /// 批量预加载，用于浏览图像列表时提升用户体验
        /// </remarks>
        void PreloadImages(string[] imageSourceIds, ImageFormat formats);

        /// <summary>
        /// 获取当前缓存大小
        /// </summary>
        int CacheSize { get; }

        /// <summary>
        /// 获取最大缓存大小
        /// </summary>
        int MaxCacheSize { get; }

        /// <summary>
        /// 设置最大缓存大小
        /// </summary>
        /// <param name="maxSize">最大缓存数量</param>
        void SetMaxCacheSize(int maxSize);

        /// <summary>
        /// 获取当前内存使用量（字节）
        /// </summary>
        long MemoryUsage { get; }

        /// <summary>
        /// 获取最大内存限制（字节）
        /// </summary>
        long MaxMemoryLimit { get; }

        /// <summary>
        /// 响应内存压力
        /// </summary>
        /// <param name="level">内存压力级别</param>
        /// <remarks>
        /// 当系统内存不足时调用，主动释放部分缓存
        /// </remarks>
        void OnMemoryPressure(MemoryPressureLevel level);

        /// <summary>
        /// 图像源被移除事件
        /// </summary>
        event EventHandler<ImageSourceRemovedEventArgs>? ImageSourceRemoved;
    }

    /// <summary>
    /// 内存压力级别
    /// </summary>
    public enum MemoryPressureLevel
    {
        /// <summary>
        /// 低压力 - 释放20%缓存
        /// </summary>
        Low,

        /// <summary>
        /// 中压力 - 释放50%缓存
        /// </summary>
        Medium,

        /// <summary>
        /// 高压力 - 释放80%缓存
        /// </summary>
        High,

        /// <summary>
        /// 紧急 - 释放所有非活动缓存
        /// </summary>
        Critical
    }

    /// <summary>
    /// 图像源移除事件参数
    /// </summary>
    public sealed class ImageSourceRemovedEventArgs : EventArgs
    {
        /// <summary>
        /// 被移除的图像源ID
        /// </summary>
        public string ImageSourceId { get; }

        /// <summary>
        /// 移除原因
        /// </summary>
        public RemovalReason Reason { get; }

        public ImageSourceRemovedEventArgs(string imageSourceId, RemovalReason reason)
        {
            ImageSourceId = imageSourceId;
            Reason = reason;
        }
    }

    /// <summary>
    /// 移除原因
    /// </summary>
    public enum RemovalReason
    {
        /// <summary>
        /// 手动移除
        /// </summary>
        Manual,

        /// <summary>
        /// LRU淘汰
        /// </summary>
        LruEviction,

        /// <summary>
        /// 内存压力
        /// </summary>
        MemoryPressure,

        /// <summary>
        /// 清空缓存
        /// </summary>
        CacheClear
    }
}
