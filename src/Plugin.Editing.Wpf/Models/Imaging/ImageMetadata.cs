using System;

namespace SunEyeVision.Plugin.Editing.Wpf.Models.Imaging
{
    /// <summary>
    /// 图像元数据
    /// </summary>
    /// <remarks>
    /// 包含图像的基本信息，不包含实际像素数据。
    /// 用于在加载图像前获取信息，支持快速预览和筛选。
    /// </remarks>
    public sealed class ImageMetadata
    {
        /// <summary>
        /// 图像宽度（像素）
        /// </summary>
        public int Width { get; init; }

        /// <summary>
        /// 图像高度（像素）
        /// </summary>
        public int Height { get; init; }

        /// <summary>
        /// 像素格式
        /// </summary>
        public PixelFormat PixelFormat { get; init; }

        /// <summary>
        /// 文件大小（字节）
        /// </summary>
        public long FileSize { get; init; }

        /// <summary>
        /// 文件路径（如果是文件源）
        /// </summary>
        public string? FilePath { get; init; }

        /// <summary>
        /// 文件名（不含路径）
        /// </summary>
        public string? FileName { get; init; }

        /// <summary>
        /// 文件扩展名
        /// </summary>
        public string? Extension { get; init; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime? CreatedTime { get; init; }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime? ModifiedTime { get; init; }

        /// <summary>
        /// 位深度
        /// </summary>
        public int BitsPerPixel { get; init; }

        /// <summary>
        /// 通道数
        /// </summary>
        public int Channels { get; init; }

        /// <summary>
        /// DPI X
        /// </summary>
        public double DpiX { get; init; }

        /// <summary>
        /// DPI Y
        /// </summary>
        public double DpiY { get; init; }

        /// <summary>
        /// 图像尺寸描述
        /// </summary>
        public string SizeDescription => $"{Width} x {Height}";

        /// <summary>
        /// 文件大小描述（人类可读格式）
        /// </summary>
        public string FileSizeDescription => FormatFileSize(FileSize);

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 创建空元数据
        /// </summary>
        public static ImageMetadata Empty => new()
        {
            Width = 0,
            Height = 0,
            PixelFormat = PixelFormat.Undefined,
            FileSize = 0
        };
    }
}
