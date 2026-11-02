using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Workflow;

/// <summary>
/// 解决方案元数据缓存
/// </summary>
/// <remarks>
/// 职责：元数据缓存管理，提高性能
///
/// 特性：
/// - LRU（最近最少使用）淘汰策略
/// - 过期时间控制
/// - 线程安全（使用 ConcurrentDictionary）
/// - 可配置缓存大小和过期时间
///
/// 设计原则（rule-002）：
/// - 命名符合视觉软件行业标准
/// - 方法使用 PascalCase，动词开头
///
/// 日志规范（rule-003）：
/// - 使用 VisionLogger 记录日志
/// - 使用适当的日志级别（Info/Success/Warning/Error）
/// </remarks>
public class SolutionCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    private readonly LinkedList<string> _lruList;
    private readonly object _lruLock;
    private readonly ILogger _logger;

    private readonly int _maxSize;
    private readonly TimeSpan _expirationTime;

    private int _hitCount;
    private int _missCount;

    /// <summary>
    /// 最大缓存数量
    /// </summary>
    public int MaxSize => _maxSize;

    /// <summary>
    /// 过期时间
    /// </summary>
    public TimeSpan ExpirationTime => _expirationTime;

    /// <summary>
    /// 当前缓存数量
    /// </summary>
    public int Count => _cache.Count;

    /// <summary>
    /// 缓存命中率
    /// </summary>
    public double HitRate
    {
        get
        {
            int total = _hitCount + _missCount;
            return total == 0 ? 0.0 : (double)_hitCount / total;
        }
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="maxSize">最大缓存数量（默认100）</param>
    /// <param name="expirationTime">过期时间（默认30分钟）</param>
    public SolutionCache(int maxSize = 100, TimeSpan? expirationTime = null)
    {
        _maxSize = maxSize;
        _expirationTime = expirationTime ?? TimeSpan.FromMinutes(30);
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        _lruList = new LinkedList<string>();
        _lruLock = new object();
        _logger = VisionLogger.Instance;

        _hitCount = 0;
        _missCount = 0;

        _logger.Log(LogLevel.Info, $"解决方案元数据缓存初始化完成: 最大数量={_maxSize}, 过期时间={_expirationTime.TotalMinutes}分钟", "SolutionCache");
    }

    /// <summary>
    /// 获取缓存的元数据
    /// </summary>
    /// <param name="key">缓存键（通常是解决方案ID）</param>
    /// <returns>元数据对象，不存在或已过期返回 null</returns>
    public SolutionMetadata? Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.Log(LogLevel.Warning, "获取缓存失败：键为空", "SolutionCache");
            return null;
        }

        if (_cache.TryGetValue(key, out var entry))
        {
            // 检查是否过期
            if (DateTime.Now - entry.CachedTime > _expirationTime)
            {
                // 过期，移除
                _cache.TryRemove(key, out _);
                RemoveFromLruList(key);
                _missCount++;
                _logger.Log(LogLevel.Info, $"缓存已过期并移除: {key}", "SolutionCache");
                return null;
            }

            // 更新LRU
            UpdateLruList(key);
            _hitCount++;

            _logger.Log(LogLevel.Info, $"缓存命中: {key}, 当前命中率={HitRate:P2}", "SolutionCache");
            return entry.Metadata.Clone();
        }

        _missCount++;
        _logger.Log(LogLevel.Info, $"缓存未命中: {key}, 当前命中率={HitRate:P2}", "SolutionCache");
        return null;
    }

    /// <summary>
    /// 设置缓存
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <param name="metadata">元数据对象</param>
    public void Set(string key, SolutionMetadata metadata)
    {
        if (string.IsNullOrEmpty(key) || metadata == null)
        {
            _logger.Log(LogLevel.Warning, "设置缓存失败：键或元数据为空", "SolutionCache");
            return;
        }

        // 检查是否需要淘汰
        if (_cache.Count >= _maxSize && !_cache.ContainsKey(key))
        {
            EvictLru();
        }

        var entry = new CacheEntry
        {
            Metadata = metadata.Clone(),
            CachedTime = DateTime.Now
        };

        _cache[key] = entry;
        UpdateLruList(key);

        _logger.Log(LogLevel.Info, $"缓存已设置: {key}, 当前缓存数量={Count}", "SolutionCache");
    }

    /// <summary>
    /// 使指定缓存失效
    /// </summary>
    /// <param name="key">缓存键</param>
    /// <returns>是否成功失效</returns>
    public bool Invalidate(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.Log(LogLevel.Warning, "使缓存失效失败：键为空", "SolutionCache");
            return false;
        }

        bool removed = _cache.TryRemove(key, out _);
        if (removed)
        {
            RemoveFromLruList(key);
            _logger.Log(LogLevel.Info, $"缓存已失效: {key}", "SolutionCache");
        }
        return removed;
    }

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    public void InvalidateAll()
    {
        int count = _cache.Count;
        _cache.Clear();

        lock (_lruLock)
        {
            _lruList.Clear();
        }

        _hitCount = 0;
        _missCount = 0;

        _logger.Log(LogLevel.Info, $"清空所有缓存: 清空了 {count} 条记录", "SolutionCache");
    }

    /// <summary>
    /// 获取缓存统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetStatistics()
    {
        return $"缓存统计: 数量={Count}, 最大={_maxSize}, 命中={_hitCount}, 未命中={_missCount}, 命中率={HitRate:P2}";
    }

    /// <summary>
    /// 清理过期缓存
    /// </summary>
    /// <returns>清理的缓存数量</returns>
    public int CleanExpired()
    {
        var expiredKeys = _cache.Where(kvp =>
                DateTime.Now - kvp.Value.CachedTime > _expirationTime)
            .Select(kvp => kvp.Key)
            .ToList();

        int cleanedCount = 0;
        foreach (var key in expiredKeys)
        {
            if (_cache.TryRemove(key, out _))
            {
                RemoveFromLruList(key);
                cleanedCount++;
            }
        }

        if (cleanedCount > 0)
        {
            _logger.Log(LogLevel.Info, $"清理过期缓存: 清理了 {cleanedCount} 条记录", "SolutionCache");
        }

        return cleanedCount;
    }

    /// <summary>
    /// 淘汰最久未使用的缓存（LRU）
    /// </summary>
    private void EvictLru()
    {
        lock (_lruLock)
        {
            if (_lruList.Count > 0)
            {
                string lruKey = _lruList.Last.Value;
                _cache.TryRemove(lruKey, out _);
                _lruList.RemoveLast();
                _logger.Log(LogLevel.Info, $"LRU淘汰: {lruKey}", "SolutionCache");
            }
        }
    }

    /// <summary>
    /// 更新LRU列表
    /// </summary>
    private void UpdateLruList(string key)
    {
        lock (_lruLock)
        {
            // 先移除（如果存在）
            var node = _lruList.Find(key);
            if (node != null)
            {
                _lruList.Remove(node);
            }

            // 添加到头部（最近使用）
            _lruList.AddFirst(key);
        }
    }

    /// <summary>
    /// 从LRU列表中移除
    /// </summary>
    private void RemoveFromLruList(string key)
    {
        lock (_lruLock)
        {
            var node = _lruList.Find(key);
            if (node != null)
            {
                _lruList.Remove(node);
            }
        }
    }

    /// <summary>
    /// 缓存条目
    /// </summary>
    private class CacheEntry
    {
        public SolutionMetadata Metadata { get; set; } = null!;
        public DateTime CachedTime { get; set; }
    }
}
