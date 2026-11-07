using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.UI.Services.Logging
{
    /// <summary>
    /// 日志事件聚合器，用于节流和批处理高频日志事件
    /// </summary>
    /// <remarks>
    /// 功能：
    /// - 事件聚合：将多个高频日志事件合并为批量事件
    /// - 节流控制：限制事件触发频率（默认100ms）
    /// - 批量处理：限制单批处理数量（默认200条）
    /// - 线程安全：使用ConcurrentQueue和锁保护共享状态
    /// - 不阻塞UI线程：使用DispatcherTimer实现真正的节流
    ///
    /// 修复说明（2026-03-16）：
    /// - 修复Dispatcher.BeginInvoke参数顺序错误
    /// - 使用DispatcherTimer替代Thread.Sleep，避免阻塞UI线程
    /// - 实现真正的节流机制，而非简单的延迟
    /// </remarks>
    public class LogEventAggregator : IDisposable
    {
        #region 常量

        /// <summary>
        /// 批量处理的最大数量
        /// </summary>
        private const int MaxBatchSize = 200;

        /// <summary>
        /// 节流延迟（毫秒）
        /// </summary>
        private const int ThrottleDelayMs = 100;

        #endregion

        #region 私有字段

        private readonly Dispatcher _dispatcher;
        private readonly ConcurrentQueue<LogEntry> _pendingLogs;
        private readonly object _lock = new();
        private DispatcherTimer? _throttleTimer;
        private readonly TimeSpan _throttleInterval;

        #endregion

        #region 属性

        /// <summary>
        /// 节流间隔
        /// </summary>
        public TimeSpan ThrottleInterval => _throttleInterval;

        /// <summary>
        /// 待处理日志数量
        /// </summary>
        public int PendingLogCount => _pendingLogs.Count;

        #endregion

        #region 事件

        /// <summary>
        /// 日志聚合事件
        /// </summary>
        public event EventHandler<LogBatchEventArgs>? LogsAggregated;

        #endregion

        #region 构造函数

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="dispatcher">UI线程调度器</param>
        /// <param name="throttleInterval">节流间隔（默认100ms）</param>
        public LogEventAggregator(Dispatcher dispatcher, TimeSpan? throttleInterval = null)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _throttleInterval = throttleInterval ?? TimeSpan.FromMilliseconds(ThrottleDelayMs);
            _pendingLogs = new ConcurrentQueue<LogEntry>();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 收集单个日志条目到待处理队列
        /// </summary>
        public void EnqueueLog(LogEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            _pendingLogs.Enqueue(entry);
            ScheduleUpdate();
        }

        /// <summary>
        /// 批量收集日志条目
        /// </summary>
        public void EnqueueLogs(IReadOnlyList<LogEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return;

            foreach (var entry in entries)
            {
                if (entry != null)
                {
                    _pendingLogs.Enqueue(entry);
                }
            }
            ScheduleUpdate();
        }

        /// <summary>
        /// 立即刷新待处理的日志
        /// </summary>
        public void Flush()
        {
            lock (_lock)
            {
                StopTimer();
                FlushLogs();
            }
        }

        /// <summary>
        /// 清空待处理队列
        /// </summary>
        public void Clear()
        {
            while (_pendingLogs.TryDequeue(out _)) { }

            lock (_lock)
            {
                StopTimer();
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                StopTimer();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 调度更新任务 - 使用DispatcherTimer实现节流
        /// </summary>
        /// <remarks>
        /// 修复说明：
        /// 1. 修复Dispatcher.BeginInvoke参数顺序错误（原bug）
        /// 2. 使用DispatcherTimer替代Thread.Sleep，避免阻塞UI线程
        /// 3. 实现真正的节流机制，而非简单的延迟
        /// </remarks>
        private void ScheduleUpdate()
        {
            lock (_lock)
            {
                // 如果已经有定时器在运行，不需要重新调度
                if (_throttleTimer != null)
                    return;

                // 创建并启动DispatcherTimer（在UI线程上执行）
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    lock (_lock)
                    {
                        if (_throttleTimer != null)
                            return;

                        _throttleTimer = new DispatcherTimer
                        {
                            Interval = _throttleInterval
                        };
                        _throttleTimer.Tick += OnThrottleTimerTick;
                        _throttleTimer.Start();
                    }
                }), DispatcherPriority.ContextIdle);
            }
        }

        /// <summary>
        /// 节流定时器触发事件
        /// </summary>
        private void OnThrottleTimerTick(object? sender, EventArgs e)
        {
            lock (_lock)
            {
                StopTimer();
                FlushLogs();
            }
        }

        /// <summary>
        /// 停止节流定时器
        /// </summary>
        private void StopTimer()
        {
            if (_throttleTimer != null)
            {
                _throttleTimer.Stop();
                _throttleTimer.Tick -= OnThrottleTimerTick;
                _throttleTimer = null;
            }
        }

        /// <summary>
        /// 批量刷新待处理的日志
        /// </summary>
        private void FlushLogs()
        {
            var batch = new List<LogEntry>(MaxBatchSize);

            lock (_lock)
            {
                // 从队列中提取日志
                while (_pendingLogs.TryDequeue(out var entry) && batch.Count < MaxBatchSize)
                {
                    batch.Add(entry);
                }

                // 如果还有日志，继续调度
                if (!_pendingLogs.IsEmpty)
                {
                    ScheduleUpdate();
                }
            }

            // 触发聚合事件（在UI线程上）
            if (batch.Count > 0)
            {
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    LogsAggregated?.Invoke(this, new LogBatchEventArgs(batch));
                }), DispatcherPriority.ContextIdle);
            }
        }

        #endregion
    }
}
