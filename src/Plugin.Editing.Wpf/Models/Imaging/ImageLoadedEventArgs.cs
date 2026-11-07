using System;

namespace SunEyeVision.Plugin.Editing.Wpf.Models.Imaging
{
    /// <summary>
    /// 图像加载完成事件参数
    /// </summary>
    public sealed class ImageLoadedEventArgs : EventArgs
    {
        /// <summary>
        /// 图像源ID
        /// </summary>
        public string ImageSourceId { get; }

        /// <summary>
        /// 已加载的图像格式
        /// </summary>
        public ImageFormat Format { get; }

        /// <summary>
        /// 加载是否成功
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// 错误信息（加载失败时）
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// 加载耗时（毫秒）
        /// </summary>
        public long LoadTimeMs { get; }

        /// <summary>
        /// 创建成功事件
        /// </summary>
        public ImageLoadedEventArgs(string imageSourceId, ImageFormat format, long loadTimeMs)
        {
            ImageSourceId = imageSourceId;
            Format = format;
            Success = true;
            LoadTimeMs = loadTimeMs;
        }

        /// <summary>
        /// 创建失败事件
        /// </summary>
        public ImageLoadedEventArgs(string imageSourceId, ImageFormat format, string errorMessage, long loadTimeMs = 0)
        {
            ImageSourceId = imageSourceId;
            Format = format;
            Success = false;
            ErrorMessage = errorMessage;
            LoadTimeMs = loadTimeMs;
        }
    }
}
