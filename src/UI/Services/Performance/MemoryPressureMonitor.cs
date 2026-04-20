using System;
using System.Diagnostics;
using System.Threading;

namespace SunEyeVision.UI.Services.Performance
{
    /// <summary>
    /// 内存压力监控器 - 响应系统内存压力自动调整缓存策略
    /// 核心优化：避免OOM，智能降级
    /// 
    /// 特点：
    /// 1. 监控系统可用内存
    /// 2. 内存压力大时自动触发缓存清理
    /// 3. 支持多级压力响应（正常、中等、高、危险）
    /// 4. 轻量化实现，无外部依赖
    /// </summary>
    public class MemoryPressureMonitor : IDisposable
    {
        /// <summary>
        /// 内存压力级别
        /// </summary>
        public enum PressureLevel
        {
            Normal,      // > 30% 可用内存
            Moderate,    // 20-30% 可用内存
            High,        // 10-20% 可用内存
            Critical     // < 10% 可用内存
        }

        /// <summary>
        /// 内存压力变化事件参数
        /// </summary>
        public class MemoryPressureEventArgs : EventArgs
        {
            public PressureLevel Level { get; set; }
            public long AvailableMemoryMB { get; set; }
            public long TotalMemoryMB { get; set; }
            public double AvailablePercent { get; set; }
            public string RecommendedAction { get; set; } = string.Empty;
        }

        /// <summary>
        /// 内存压力变化事件
        /// </summary>
        public event EventHandler<MemoryPressureEventArgs>? MemoryPressureChanged;

        private Timer? _monitorTimer;
        private PressureLevel _currentLevel = PressureLevel.Normal;
        private readonly object _lock = new();
        private bool _disposed = false;

        // 配置参数
        private const int MONITOR_INTERVAL_MS = 2000; // 2秒检查一次
        private const long MIN_MEMORY_MB = 100; // 最小保留内存

        /// <summary>
        /// 当前内存压力级别
        /// </summary>
        public PressureLevel CurrentLevel => _currentLevel;

        /// <summary>
        /// 是否启用监控
        /// </summary>
        public bool IsEnabled { get; private set; }

        /// <summary>
        /// 开始监控
        /// </summary>
        public void Start()
        {
            if (IsEnabled) return;

            IsEnabled = true;
            _monitorTimer = new Timer(CheckMemoryPressure, null, 
                MONITOR_INTERVAL_MS, MONITOR_INTERVAL_MS);
}

        /// <summary>
        /// 停止监控
        /// </summary>
        public void Stop()
        {
            if (!IsEnabled) return;

            IsEnabled = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;
}

        /// <summary>
        /// 检查内存压力
        /// </summary>
        private void CheckMemoryPressure(object? state)
        {
            try
            {
                var (availableMB, totalMB, availablePercent) = GetMemoryInfo();
                var newLevel = CalculatePressureLevel(availablePercent);

                if (newLevel != _currentLevel)
                {
                    var oldLevel = _currentLevel;
                    lock (_lock)
                    {
                        _currentLevel = newLevel;
                    }

                    var args = new MemoryPressureEventArgs
                    {
                        Level = newLevel,
                        AvailableMemoryMB = availableMB,
                        TotalMemoryMB = totalMB,
                        AvailablePercent = availablePercent,
                        RecommendedAction = GetRecommendedAction(newLevel)
                    };

                    MemoryPressureChanged?.Invoke(this, args);
                }
            }
            catch (Exception ex)
            {
                // 静默处理
            }
        }

        /// <summary>
        /// 获取内存信息
        /// </summary>
        private (long availableMB, long totalMB, double availablePercent) GetMemoryInfo()
        {
            using var process = Process.GetCurrentProcess();
            var totalAvailableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            var estimatedAvailable = Math.Max(0, totalAvailableMemory - process.WorkingSet64 / (1024 * 1024));
            var availablePercent = totalAvailableMemory > 0 
                ? (double)estimatedAvailable / totalAvailableMemory * 100 
                : 100;

            return (estimatedAvailable, totalAvailableMemory, availablePercent);
        }

        /// <summary>
        /// 计算压力级别（优化阈值，减少误触发）
        /// Normal: > 30% 
        /// Moderate: 20-30%
        /// High: 10-20%
        /// Critical: < 10%
        /// </summary>
        private PressureLevel CalculatePressureLevel(double availablePercent)
        {
            if (availablePercent > 30) return PressureLevel.Normal;
            if (availablePercent > 20) return PressureLevel.Moderate;
            if (availablePercent > 10) return PressureLevel.High;
            return PressureLevel.Critical;
        }

        /// <summary>
        /// 获取推荐操作
        /// </summary>
        private string GetRecommendedAction(PressureLevel level)
        {
            return level switch
            {
                PressureLevel.Normal => "正常加载",
                PressureLevel.Moderate => "减少预读取数量",
                PressureLevel.High => "清理弱引用缓存",
                PressureLevel.Critical => "强制GC，清空缓存",
                _ => "未知"
            };
        }

        /// <summary>
        /// 手动触发内存检查
        /// </summary>
        public MemoryPressureEventArgs CheckNow()
        {
            var (availableMB, totalMB, availablePercent) = GetMemoryInfo();
            var level = CalculatePressureLevel(availablePercent);

            return new MemoryPressureEventArgs
            {
                Level = level,
                AvailableMemoryMB = availableMB,
                TotalMemoryMB = totalMB,
                AvailablePercent = availablePercent,
                RecommendedAction = GetRecommendedAction(level)
            };
        }

        /// <summary>
        /// 响应内存压力（建议在压力变化时调用）
        /// </summary>
        public void RespondToPressure(PressureLevel level, Action? onHigh = null, Action? onCritical = null)
        {
            switch (level)
            {
                case PressureLevel.High:
                    // 高压力：清理弱引用缓存，减少预读取
                    onHigh?.Invoke();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Optimized);
break;

                case PressureLevel.Critical:
                    // 危险：强制GC，清空缓存
                    onCritical?.Invoke();
                    GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
break;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _disposed = true;
            }
        }
    }
}
