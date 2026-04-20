using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;

namespace SunEyeVision.UI.Services.Performance
{
    /// <summary>
    /// 批量UI更新器 - 合并UI更新请求，减少刷新频率
    /// 使用DispatcherTimer实现批量更新
    /// </summary>
    public class BatchUIUpdater : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly Queue<Action> _pendingUpdates;
        private readonly object _lock = new object();
        private readonly int _maxBatchSize;
        private bool _disposed;

        /// <summary>
        /// 创建批量UI更新器
        /// </summary>
        /// <param name="updateInterval">更新间隔（毫秒，默认16ms约60fps）</param>
        /// <param name="maxBatchSize">单次最大处理数量（默认10）</param>
        public BatchUIUpdater(int updateInterval = 16, int maxBatchSize = 10)
        {
            _pendingUpdates = new Queue<Action>();
            _maxBatchSize = maxBatchSize;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(updateInterval),
                IsEnabled = false
            };

            _timer.Tick += OnTimerTick;
        }

        /// <summary>
        /// 添加UI更新请求
        /// </summary>
        /// <param name="updateAction">更新操作</param>
        public void Enqueue(Action updateAction)
        {
            if (_disposed)
                return;

            lock (_lock)
            {
                _pendingUpdates.Enqueue(updateAction);

                // 启动定时器（如果未启动）
                if (!_timer.IsEnabled)
                {
                    _timer.Start();
                }
            }
        }

        /// <summary>
        /// 立即执行所有待处理的更新
        /// </summary>
        public void Flush()
        {
            if (_disposed)
                return;

            List<Action> toProcess;

            lock (_lock)
            {
                if (_pendingUpdates.Count == 0)
                    return;

                toProcess = new List<Action>(_pendingUpdates);
                _pendingUpdates.Clear();
            }

            // 在UI线程执行
            Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var action in toProcess)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
}
                }
            });
        }

        /// <summary>
        /// 定时器触发
        /// </summary>
        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_disposed)
                return;

            List<Action> toProcess;

            lock (_lock)
            {
                if (_pendingUpdates.Count == 0)
                {
                    _timer.Stop();
                    return;
                }

                // 批量取出一部分
                toProcess = new List<Action>();
                int count = Math.Min(_pendingUpdates.Count, _maxBatchSize);

                for (int i = 0; i < count; i++)
                {
                    toProcess.Add(_pendingUpdates.Dequeue());
                }
            }

            // 执行更新
            foreach (var action in toProcess)
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
}
            }

            // 如果还有待处理的，继续
            lock (_lock)
            {
                if (_pendingUpdates.Count == 0)
                {
                    _timer.Stop();
                }
            }
        }

        /// <summary>
        /// 获取待处理更新数量
        /// </summary>
        public int PendingCount
        {
            get
            {
                lock (_lock)
                {
                    return _pendingUpdates.Count;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _timer.Stop();
            Flush();
            _pendingUpdates.Clear();
        }
    }
}
