using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace SunEyeVision.UI.Services.Thumbnail
{
    /// <summary>
    /// LRU图像缓存（全分辨率图像，上限 30）
    /// </summary>
    public class ImageCache
    {
        private readonly int _maxCacheSize;
        private readonly Dictionary<string, LinkedListNode<CacheEntry>> _cacheMap;
        private readonly LinkedList<CacheEntry> _lruList;
        private readonly long _byteBudget;
        private long _bytes;
        public long DecodedBytes { get { lock (_cacheMap) return _bytes; } }

        public ImageCache(int maxCacheSize = 30, long byteBudget = 64 * 1024 * 1024)
        {
            if (maxCacheSize <= 0 || byteBudget <= 0) throw new ArgumentOutOfRangeException(nameof(maxCacheSize));
            _maxCacheSize = maxCacheSize;
            _byteBudget = byteBudget;
            _cacheMap = new Dictionary<string, LinkedListNode<CacheEntry>>(StringComparer.OrdinalIgnoreCase);
            _lruList = new LinkedList<CacheEntry>();
        }

        internal static string ReadVersion(string path)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
        }

        /// <summary>
        /// 查询缓存（命中则提升 LRU 优先级）。调用方须在外部加锁（仅保护字典，短临界区）。
        /// </summary>
        public BitmapSource? Get(string filePath, string? version = null)
        {
            version ??= ReadVersion(filePath);
            lock (_cacheMap)
            {
            if (_cacheMap.TryGetValue(filePath, out var node))
            {
                if (node.Value.Version != version) { Remove(filePath); return null; }
                _lruList.Remove(node);
                _lruList.AddFirst(node);
                return node.Value.Bitmap;
            }
            return null;
            }
        }

        /// <summary>
        /// 写入缓存（含 LRU 淘汰）。调用方须在外部加锁。已在缓存中则不重复写入。
        /// </summary>
        public void Set(string filePath, BitmapSource? bitmap, string? version = null)
        {
            if (bitmap == null) return;
            version ??= ReadVersion(filePath);
            lock (_cacheMap)
            {
            Remove(filePath);

            var entry = new CacheEntry(filePath, bitmap, version);
            if (entry.Bytes > _byteBudget) return;
            var newNode = _lruList.AddFirst(entry);
            _cacheMap[filePath] = newNode;
            _bytes += entry.Bytes;

            while (_lruList.Count > _maxCacheSize || _bytes > _byteBudget)
            {
                var lastNode = _lruList.Last;
                if (lastNode != null)
                {
                    _cacheMap.Remove(lastNode.Value.FilePath);
                    _bytes -= lastNode.Value.Bytes;
                    _lruList.RemoveLast();
                }
            }
            }
        }

        public void Remove(string path)
        {
            lock (_cacheMap)
                if (_cacheMap.Remove(path, out var node)) { _bytes -= node.Value.Bytes; _lruList.Remove(node); }
        }

        public void Clear()
        {
            lock (_cacheMap)
            {
                _cacheMap.Clear();
                _lruList.Clear();
                _bytes = 0;
            }
        }

        private class CacheEntry
        {
            public string FilePath { get; }
            public BitmapSource Bitmap { get; }
            public string Version { get; }
            public long Bytes { get; }

            public CacheEntry(string filePath, BitmapSource bitmap, string version)
            {
                FilePath = filePath;
                Bitmap = bitmap;
                Version = version;
                Bytes = ((long)bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8 * bitmap.PixelHeight;
            }
        }
    }

}
