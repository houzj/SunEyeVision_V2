using SunEyeVision.UI.Shared;
using System;
using System.IO;
using System.Windows.Media.Imaging;

namespace SunEyeVision.UI.Services.Thumbnail;

/// <summary>Shared file-preview decoding and content-version caches. No control or workflow ownership.</summary>
public static class LocalImageAssetCache
{
        // 全分辨率图像缓存（LRU，上限 30）——保留，供 ImageInfo.FullImage 懒加载
        private static readonly ImageCache s_fullImageCache = new ImageCache(maxCacheSize: 30);

        private static readonly PreviewBitmapCache s_thumbnailCache = new(DecodeThumbnailCore);
        public static PreviewBitmapCacheStats ThumbnailCacheStatistics => s_thumbnailCache.Statistics;

        internal static System.Threading.Tasks.Task<BitmapSource?> LoadThumbnailAsync(string filePath,
            System.Threading.CancellationToken token = default) => s_thumbnailCache.LoadAsync(filePath, token);

        internal static BitmapImage? LoadThumbnail(string filePath, System.Threading.CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return null;
            try { return LoadThumbnailAsync(filePath, token).GetAwaiter().GetResult() as BitmapImage; }
            catch (OperationCanceledException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        private static BitmapSource DecodeThumbnailCore(string filePath)
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var header = BitmapFrame.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            var landscape = header.PixelWidth >= header.PixelHeight;
            stream.Position = 0;
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            if (landscape) bitmap.DecodePixelWidth = 100;
            else bitmap.DecodePixelHeight = 100;
            // Fresh streams have no URI cache key. Content versions are owned by PreviewBitmapCache.
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        /// <summary>
        /// 全分辨率图像加载（简化版）：ImageInfo.FullImage 懒加载入口。
        /// 用新文件流 + OnLoad 解码，版本由内容哈希校验。
        /// </summary>
        public static BitmapImage? LoadImageOptimized(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            // The legacy display caller schedules this file/decoder work off the dispatcher.
            try
            {
                var version = ImageCache.ReadVersion(filePath);
                var cached = s_fullImageCache.Get(filePath, version);
                if (cached != null) return cached as BitmapImage;
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = stream;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                if (ImageCache.ReadVersion(filePath) != version) return null;
                s_fullImageCache.Set(filePath, bitmap, version);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public static void InvalidateImageCache(string path)
        {
            s_fullImageCache.Remove(path);
            s_thumbnailCache.Invalidate(path);
        }

}
