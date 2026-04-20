using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Services.Logging
{
    /// <summary>
    /// UI日志写入器 - 高性能UI显示
    /// </summary>
    /// <remarks>
    /// 特性：
    /// - UI线程安全
    /// - 循环缓冲区：O(1) 添加/移除操作
    /// - 批量添加优化：单次事件通知
    /// - 增量统计：避免遍历计算
    /// </remarks>
    public class UILogWriter : ILogWriter
    {
        #region 常量

        /// <summary>
        /// 最大显示日志条数
        /// </summary>
        private const int MaxDisplayLogs = 5000;

        #endregion

        #region 私有字段

        private readonly CircularBufferLogCollection _logEntries;
        private readonly Dispatcher _dispatcher;
        private readonly LogEventAggregator _eventAggregator;
        private bool _isDisposed;

        #endregion

        #region 属性

        public string Name => "UILogger";
        public string Description => "UI界面日志写入器";

        public LogTarget Channel => LogTarget.UI;
        public LogLevel MinLevel { get; set; } = LogLevel.Info;
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 日志条目集合（用于UI绑定）- 高性能循环缓冲区
        /// </summary>
        public CircularBufferLogCollection LogEntries => _logEntries;

        /// <summary>
        /// 当前日志数量
        /// </summary>
        public int Count => _logEntries.Count;

        /// <summary>
        /// 错误数量（O(1)访问）
        /// </summary>
        public int ErrorCount => _logEntries.ErrorCount;

        /// <summary>
        /// 警告数量（O(1)访问）
        /// </summary>
        public int WarningCount => _logEntries.WarningCount;

        /// <summary>
        /// 总入队数量（不受容量限制影响）
        /// </summary>
        public long TotalEnqueued => _logEntries.TotalEnqueued;

        #endregion

        #region 事件

        /// <summary>
        /// 日志添加事件（UI线程触发）
        /// </summary>
        public event EventHandler<LogBatchEventArgs>? LogsAdded;

        /// <summary>
        /// 日志清空事件
        /// </summary>
        public event EventHandler? LogsCleared;

        /// <summary>
        /// 统计信息变更事件
        /// </summary>
        public event EventHandler? StatisticsChanged;

        #endregion

        #region 构造函数

        public UILogWriter()
        {
            _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _logEntries = new CircularBufferLogCollection(MaxDisplayLogs, _dispatcher);

            // 创建事件聚合器（100ms节流间隔）
            _eventAggregator = new LogEventAggregator(_dispatcher, TimeSpan.FromMilliseconds(100));

            // 订阅聚合器事件
            _eventAggregator.LogsAggregated += OnLogsAggregated;

            // 订阅集合变更事件以更新统计
            _logEntries.CollectionChanged += OnLogEntriesCollectionChanged;
        }

        #endregion

        #region ILogWriter 实现

        public void Write(LogEntry entry)
        {
            if (!IsEnabled || entry.Level < MinLevel)
                return;

            AddToCollection(new[] { entry });
        }

        public void WriteBatch(IReadOnlyList<LogEntry> entries)
        {
            if (!IsEnabled || entries.Count == 0)
            {
                return;
            }

            var filtered = entries.Where(e => e.Level >= MinLevel).ToList();
            if (filtered.Count > 0)
            {
                AddToCollection(filtered);
            }
        }

        public void Flush()
        {
            // UI不需要刷新
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // 取消订阅聚合器事件
            _eventAggregator.LogsAggregated -= OnLogsAggregated;

            // 清空聚合器
            _eventAggregator.Clear();

            // 取消订阅集合变更事件
            _logEntries.CollectionChanged -= OnLogEntriesCollectionChanged;

            // 清空日志
            Clear();
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 清空日志
        /// </summary>
        public void Clear()
        {
            if (_dispatcher.CheckAccess())
            {
                _logEntries.Clear();
                LogsCleared?.Invoke(this, EventArgs.Empty);
                StatisticsChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _dispatcher.Invoke(() => Clear());
            }
        }

        /// <summary>
        /// 获取统计信息 - O(1)操作
        /// </summary>
        public (int Count, int Errors, int Warnings, int Successes) GetStatistics()
        {
            return (
                _logEntries.Count,
                _logEntries.ErrorCount,
                _logEntries.WarningCount,
                _logEntries.SuccessCount
            );
        }

        #endregion

        #region 私有方法

        private void AddToCollection(IReadOnlyList<LogEntry> entries)
        {
            if (_dispatcher.CheckAccess())
            {
                // 使用聚合器代替直接添加和事件触发
                // 聚合器会批量处理并延迟触发事件
                _eventAggregator.EnqueueLogs(entries);
            }
            else
            {
                _dispatcher.Invoke(() => AddToCollection(entries));
            }
        }

        /// <summary>
        /// 聚合器日志批量处理事件
        /// </summary>
        private void OnLogsAggregated(object? sender, LogBatchEventArgs e)
        {
            // 批量添加到集合
            _logEntries.AddRange(e.Logs);

            // 触发外部订阅者
            LogsAdded?.Invoke(this, e);
        }

        private void OnLogEntriesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // 触发统计变更事件
            StatisticsChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion
    }

    /// <summary>
    /// 日志批量事件参数
    /// </summary>
    public class LogBatchEventArgs : EventArgs
    {
        public IReadOnlyList<LogEntry> Logs { get; }

        public LogBatchEventArgs(IReadOnlyList<LogEntry> logs)
        {
            Logs = logs;
        }
    }
}
