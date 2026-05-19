using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Imaging;

namespace SunEyeVision.Plugin.SDK.Models.Imaging
{
    /// <summary>
    /// 图像源接口 - 统一的图像数据访问抽象
    /// </summary>
    /// <remarks>
    /// <para>
    /// IImageSource是SunEyeVision图像系统的核心抽象，提供统一的图像数据访问接口。
    /// 设计目标：
    /// </para>
    /// <list type="bullet">
    /// <item><description>延迟加载：仅在需要时加载特定格式</description></item>
    /// <item><description>多格式支持：缩略图、显示图像、Mat处理格式</description></item>
    /// <item><description>内存优化：按需加载，支持资源释放</description></item>
    /// <item><description>线程安全：支持多线程访问</description></item>
    /// <item><description>预加载：支持后台预加载提升用户体验</description></item>
    /// </list>
    /// 
    /// <para>使用场景：</para>
    /// <list type="bullet">
    /// <item><description>图像预览器 - 使用Thumbnail格式</description></item>
    /// <item><description>主显示区域 - 使用FullImage格式</description></item>
    /// <item><description>算法处理节点 - 使用Mat格式</description></item>
    /// </list>
    /// </remarks>
    public interface IImageSource : IDisposable
    {
        /// <summary>
        /// 图像源唯一标识符
        /// </summary>
        string Id { get; }

        /// <summary>
        /// 图像源类型标识
        /// </summary>
        /// <remarks>
        /// 用于标识图像源的类型，如 "File", "Memory", "Camera" 等
        /// </remarks>
        string SourceType { get; }

        /// <summary>
        /// 图像元数据（不包含像素数据）
        /// </summary>
        /// <remarks>
        /// 用于快速获取图像信息，无需加载完整图像
        /// </remarks>
        ImageMetadata? Metadata { get; }

        /// <summary>
        /// 获取缩略图
        /// </summary>
        /// <param name="size">缩略图尺寸（宽高相等）</param>
        /// <returns>缩略图BitmapSource，加载失败返回null</returns>
        BitmapSource? GetThumbnail(int size = 60);

        /// <summary>
        /// 获取完整显示图像
        /// </summary>
        /// <returns>完整显示图像BitmapSource，加载失败返回null</returns>
        BitmapSource? GetFullImage();

        /// <summary>
        /// 获取IVisionImage格式图像
        /// </summary>
        /// <returns>IVisionImage对象，加载失败返回null</returns>
        IVisionImage? GetMat();

        /// <summary>
        /// 异步获取缩略图
        /// </summary>
        /// <param name="size">缩略图尺寸</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>缩略图BitmapSource，加载失败返回null</returns>
        Task<BitmapSource?> GetThumbnailAsync(int size = 60, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步获取完整显示图像
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>完整显示图像BitmapSource，加载失败返回null</returns>
        Task<BitmapSource?> GetFullImageAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步获取IVisionImage格式图像
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>IVisionImage对象，加载失败返回null</returns>
        Task<IVisionImage?> GetMatAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取指定格式的加载状态
        /// </summary>
        /// <param name="format">图像格式</param>
        /// <returns>加载状态</returns>
        ImageLoadState GetLoadState(ImageFormat format);

        /// <summary>
        /// 预加载指定格式的图像
        /// </summary>
        /// <param name="formats">要预加载的格式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <remarks>
        /// 在后台异步加载图像，提升后续访问速度
        /// </remarks>
        void Preload(ImageFormat formats, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步预加载指定格式的图像
        /// </summary>
        /// <param name="formats">要预加载的格式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>预加载任务</returns>
        Task PreloadAsync(ImageFormat formats, CancellationToken cancellationToken = default);

        /// <summary>
        /// 释放指定格式的图像数据
        /// </summary>
        /// <param name="formats">要释放的格式</param>
        /// <remarks>
        /// 释放内存，但保留元数据。下次访问时会重新加载
        /// </remarks>
        void Release(ImageFormat formats);

        /// <summary>
        /// 图像加载完成事件
        /// </summary>
        event EventHandler<ImageLoadedEventArgs>? ImageLoaded;
    }
}
