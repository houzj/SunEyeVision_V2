using HalconDotNet;
using System;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.SDK.Models.Imaging
{
    public interface IImageSourceManager : IDisposable
    {
        IImageSource CreateFromFile(string filePath);
        IImageSource CreateFromMemory(HImage image, string? sourceId = null);
        IImageSource CreateFromBitmapSource(BitmapSource bitmapSource, string? sourceId = null);
        IImageSource? GetImageSource(string imageSourceId);
        bool Contains(string imageSourceId);
        bool Remove(string imageSourceId);
        void Clear();
        void PreloadImages(string[] imageSourceIds, ImageFormat formats);
        int CacheSize { get; }
        int MaxCacheSize { get; }
        void SetMaxCacheSize(int maxSize);
        long MemoryUsage { get; }
        long MaxMemoryLimit { get; }
        void OnMemoryPressure(MemoryPressureLevel level);
        event EventHandler<ImageSourceRemovedEventArgs>? ImageSourceRemoved;
    }

    public enum MemoryPressureLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    public sealed class ImageSourceRemovedEventArgs : EventArgs
    {
        public string ImageSourceId { get; }
        public RemovalReason Reason { get; }

        public ImageSourceRemovedEventArgs(string imageSourceId, RemovalReason reason)
        {
            ImageSourceId = imageSourceId;
            Reason = reason;
        }
    }

    public enum RemovalReason
    {
        Manual,
        LruEviction,
        MemoryPressure,
        CacheClear
    }
}
