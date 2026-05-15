using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Imaging;
using SunEyeVision.UI.Extensions;

namespace SunEyeVision.UI.Services.Images
{
    /// <summary>
    /// 图像源管理器 - LRU缓存管理实现
    /// </summary>
    /// <remarks>
    /// 提供图像源的创建、缓存和管理功能，实现LRU淘汰策略。
    /// 全局单例模式，统一管理所有图像源。
    /// </remarks>
    public sealed class ImageSourceManager : IImageSourceManager
    {
        // ========== 全局单例 ==========
        private static readonly Lazy<ImageSourceManager> _instance = 
            new(() => new ImageSourceManager());
        
        /// <summary>
        /// 全局单例实例
        /// </summary>
        public static ImageSourceManager Instance => _instance.Value;
        
        private readonly object _lock = new();

        // LRU缓存 - 使用LinkedList和Dictionary实现O(1)访问
        private readonly LinkedList<string> _lruList = new();
        private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new();
        private readonly Dictionary<string, IImageSource> _cache = new();

        // 配置
        private int _maxCacheSize = 100;
        private long _maxMemoryLimit = 500 * 1024 * 1024; // 500MB

        // 内存使用统计
        private long _estimatedMemoryUsage;

        // 是否已释放
        private bool _disposed;

        /// <summary>
        /// 当前缓存大小
        /// </summary>
        public int CacheSize
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <summary>
        /// 最大缓存大小
        /// </summary>
        public int MaxCacheSize => _maxCacheSize;

        /// <summary>
        /// 当前内存使用量估算
        /// </summary>
        public long MemoryUsage => _estimatedMemoryUsage;

        /// <summary>
        /// 最大内存限制
        /// </summary>
        public long MaxMemoryLimit => _maxMemoryLimit;

        /// <summary>
        /// 图像源移除事件
        /// </summary>
        public event EventHandler<ImageSourceRemovedEventArgs>? ImageSourceRemoved;

        /// <summary>
        /// 创建文件图像源
        /// </summary>
        public IImageSource CreateFromFile(string filePath)
        {
            ThrowIfDisposed();

            var id = $"file://{System.IO.Path.GetFullPath(filePath)}";

            lock (_lock)
            {
                // 检查是否已缓存
                if (_cache.TryGetValue(id, out var existing))
                {
                    UpdateLru(id);
                    return existing;
                }

                // 创建新的图像源
                var imageSource = new FileImageSource(filePath, id);

                // 添加到缓存
                AddToCache(imageSource);

                return imageSource;
            }
        }

        /// <summary>
        /// 创建内存图像源
        /// </summary>
        public IImageSource CreateFromMemory(HImage image, string? sourceId = null)
        {
            ThrowIfDisposed();

            if (image == null)
                throw new ArgumentNullException(nameof(image));

            var id = sourceId ?? $"memory://{Guid.NewGuid():N}";

            lock (_lock)
            {
                // 检查是否已缓存
                if (sourceId != null && _cache.TryGetValue(id, out var existing))
                {
                    UpdateLru(id);
                    return existing;
                }

                // 创建新的图像源
                var imageSource = new MemoryImageSource(image, id, ownsImage: true);

                // 添加到缓存
                AddToCache(imageSource);

                // 估算内存使用
                _estimatedMemoryUsage += EstimateImageSize(image);

                return imageSource;
            }
        }

        /// <summary>
        /// 从BitmapSource创建内存图像源
        /// </summary>
        public IImageSource CreateFromBitmapSource(BitmapSource bitmapSource, string? sourceId = null)
        {
            ThrowIfDisposed();

            if (bitmapSource == null)
                throw new ArgumentNullException(nameof(bitmapSource));

            var id = sourceId ?? $"memory://{Guid.NewGuid():N}";

            lock (_lock)
            {
                // 检查是否已缓存
                if (sourceId != null && _cache.TryGetValue(id, out var existing))
                {
                    UpdateLru(id);
                    return existing;
                }

                // 创建新的图像源
                var imageSource = new MemoryImageSource(bitmapSource, id);

                // 添加到缓存
                AddToCache(imageSource);

                // 估算内存使用
                _estimatedMemoryUsage += EstimateBitmapSourceSize(bitmapSource);

                return imageSource;
            }
        }

        /// <summary>
        /// 获取图像源
        /// </summary>
        public IImageSource? GetImageSource(string imageSourceId)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                if (_cache.TryGetValue(imageSourceId, out var imageSource))
                {
                    UpdateLru(imageSourceId);
                    return imageSource;
                }
                return null;
            }
        }

        /// <summary>
        /// 检查图像源是否存在
        /// </summary>
        public bool Contains(string imageSourceId)
        {
            lock (_lock)
            {
                return _cache.ContainsKey(imageSourceId);
            }
        }

        /// <summary>
        /// 移除图像源
        /// </summary>
        public bool Remove(string imageSourceId)
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                if (!_cache.TryGetValue(imageSourceId, out var imageSource))
                    return false;

                // 从缓存移除
                _cache.Remove(imageSourceId);

                // 从LRU列表移除
                if (_lruNodes.TryGetValue(imageSourceId, out var node))
                {
                    _lruList.Remove(node);
                    _lruNodes.Remove(imageSourceId);
                }

                // 更新内存估算
                UpdateEstimatedMemory(imageSource, isRemoving: true);

                // 释放资源
                imageSource.Dispose();

                // 触发事件
                OnImageSourceRemoved(imageSourceId, RemovalReason.Manual);

                return true;
            }
        }

        /// <summary>
        /// 清空所有图像源
        /// </summary>
        public void Clear()
        {
            ThrowIfDisposed();

            lock (_lock)
            {
                var ids = _cache.Keys.ToList();

                foreach (var id in ids)
                {
                    if (_cache.TryGetValue(id, out var imageSource))
                    {
                        imageSource.Dispose();
                        OnImageSourceRemoved(id, RemovalReason.CacheClear);
                    }
                }

                _cache.Clear();
                _lruList.Clear();
                _lruNodes.Clear();
                _estimatedMemoryUsage = 0;
            }
        }

        /// <summary>
        /// 预加载图像源列表
        /// </summary>
        public void PreloadImages(string[] imageSourceIds, ImageFormat formats)
        {
            ThrowIfDisposed();

            if (imageSourceIds == null || imageSourceIds.Length == 0)
                return;

            Task.Run(() =>
            {
                foreach (var id in imageSourceIds)
                {
                    IImageSource? imageSource;
                    lock (_lock)
                    {
                        _cache.TryGetValue(id, out imageSource);
                    }

                    if (imageSource != null)
                    {
                        imageSource.Preload(formats);
                    }
                }
            });
        }

        /// <summary>
        /// 设置最大缓存大小
        /// </summary>
        public void SetMaxCacheSize(int maxSize)
        {
            if (maxSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSize), "最大缓存大小必须大于0");

            lock (_lock)
            {
                _maxCacheSize = maxSize;
                EvictIfNeeded();
            }
        }

        /// <summary>
        /// 响应内存压力
        /// </summary>
        public void OnMemoryPressure(MemoryPressureLevel level)
        {
            lock (_lock)
            {
                int releaseCount = level switch
                {
                    MemoryPressureLevel.Low => (int)(_cache.Count * 0.2),
                    MemoryPressureLevel.Medium => (int)(_cache.Count * 0.5),
                    MemoryPressureLevel.High => (int)(_cache.Count * 0.8),
                    MemoryPressureLevel.Critical => _cache.Count,
                    _ => 0
                };

                EvictItems(releaseCount, RemovalReason.MemoryPressure);
            }
        }

        /// <summary>
        /// 添加到缓存
        /// </summary>
        private void AddToCache(IImageSource imageSource)
        {
            // 检查是否需要淘汰
            EvictIfNeeded();

            // 添加到缓存
            _cache[imageSource.Id] = imageSource;

            // 添加到LRU列表头部
            var node = _lruList.AddFirst(imageSource.Id);
            _lruNodes[imageSource.Id] = node;
        }

        /// <summary>
        /// 更新LRU顺序
        /// </summary>
        private void UpdateLru(string id)
        {
            if (_lruNodes.TryGetValue(id, out var node))
            {
                _lruList.Remove(node);
                _lruList.AddFirst(node);
            }
        }

        /// <summary>
        /// 检查并执行淘汰
        /// </summary>
        private void EvictIfNeeded()
        {
            // 基于数量淘汰
            while (_cache.Count >= _maxCacheSize)
            {
                EvictOne(RemovalReason.LruEviction);
            }

            // 基于内存淘汰
            while (_estimatedMemoryUsage > _maxMemoryLimit && _cache.Count > 10)
            {
                EvictOne(RemovalReason.LruEviction);
            }
        }

        /// <summary>
        /// 淘汰指定数量的项
        /// </summary>
        private void EvictItems(int count, RemovalReason reason)
        {
            for (int i = 0; i < count && _cache.Count > 0; i++)
            {
                EvictOne(reason);
            }
        }

        /// <summary>
        /// 淘汰一个最久未使用的项
        /// </summary>
        private void EvictOne(RemovalReason reason)
        {
            if (_lruList.Count == 0)
                return;

            var lastId = _lruList.Last.Value;

            if (_cache.TryGetValue(lastId, out var imageSource))
            {
                // 更新内存估算
                UpdateEstimatedMemory(imageSource, isRemoving: true);

                // 从缓存移除
                _cache.Remove(lastId);
                _lruList.RemoveLast();
                _lruNodes.Remove(lastId);

                // 释放资源
                imageSource.Dispose();

                // 触发事件
                OnImageSourceRemoved(lastId, reason);
            }
        }

        /// <summary>
        /// 更新估算内存使用
        /// </summary>
        private void UpdateEstimatedMemory(IImageSource imageSource, bool isRemoving)
        {
            if (imageSource.Metadata == null) return;

            var size = imageSource.Metadata.Width * imageSource.Metadata.Height *
                       imageSource.Metadata.Channels;

            if (isRemoving)
            {
                _estimatedMemoryUsage -= size;
                if (_estimatedMemoryUsage < 0)
                    _estimatedMemoryUsage = 0;
            }
            else
            {
                _estimatedMemoryUsage += size;
            }
        }

        /// <summary>
        /// 估算Mat大小
        /// </summary>
        private static long EstimateImageSize(HImage image)
        {
            var info = image.GetInfo();
            return info.Width * info.Height * info.Channels;
        }

        /// <summary>
        /// 估算BitmapSource大小
        /// </summary>
        private static long EstimateBitmapSourceSize(BitmapSource bitmap)
        {
            return bitmap.PixelWidth * bitmap.PixelHeight * (bitmap.Format.BitsPerPixel / 8);
        }

        /// <summary>
        /// 触发图像源移除事件
        /// </summary>
        private void OnImageSourceRemoved(string imageSourceId, RemovalReason reason)
        {
            ImageSourceRemoved?.Invoke(this, new ImageSourceRemovedEventArgs(imageSourceId, reason));
        }

        /// <summary>
        /// 抛出已释放异常
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ImageSourceManager));
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed) return;

                Clear();
                _disposed = true;
            }
        }
    }
}
