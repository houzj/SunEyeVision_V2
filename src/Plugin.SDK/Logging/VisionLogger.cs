using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 核心日志服务 - 高性能、多写入器、异步队列
    /// </summary>
    /// <remarks>
    /// 特性：
    /// - 高性能：ConcurrentQueue 异步入队，写入延迟 < 0.01ms
    /// - 多写入器：支持同时输出到文件、UI、调试等多种目标
    /// - 批量处理：定时批量刷新，减少 IO 操作
    /// - 内存安全：队列容量限制，自动丢弃旧日志
    /// - 优雅关闭：支持等待队列清空后关闭
    /// </remarks>
    public sealed class VisionLogger : ILogger, ILoggerProvider, IDisposable
    {
        #region 常量配置

        /// <summary>
        /// 队列最大容量（防止内存溢出）
        /// </summary>
        private const int MaxQueueSize = 50000;

        /// <summary>
        /// 批量处理大小
        /// </summary>
        private const int BatchSize = 200;

        /// <summary>
        /// 批处理间隔（毫秒）
        /// </summary>
        private const int BatchIntervalMs = 50;

        /// <summary>
        /// 队列告警阈值
        /// </summary>
        private const int QueueWarningThreshold = 30000;

        #endregion

        #region 私有字段

        private readonly ConcurrentQueue<LogEntry> _queue;
        private readonly List<ILogWriter> _writers;
        private readonly ReaderWriterLockSlim _writersLock;
        private readonly CancellationTokenSource _cts;
        private readonly Task _processingTask;
        private readonly SemaphoreSlim _flushSignal;
        
        private long _idCounter;
        private long _totalEnqueued;
        private long _totalDropped;
        private LogLevel _minLevel = LogLevel.Info;
        private bool _isDisposed;
        private string? _defaultSource;

        // 静态实例
        private static VisionLogger? _instance;
        private static readonly object _instanceLock = new();

        #endregion

        #region 属性

        /// <summary>
        /// 获取全局实例
        /// </summary>
        public static VisionLogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        _instance ??= new VisionLogger();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// 全局日志器（ILoggerProvider 实现）
        /// </summary>
        public ILogger GlobalLogger => this;

        /// <summary>
        /// 最低日志级别
        /// </summary>
        public LogLevel MinLevel
        {
            get => _minLevel;
            set => _minLevel = value;
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 当前队列大小
        /// </summary>
        public int QueueSize => _queue.Count;

        /// <summary>
        /// 总入队数量
        /// </summary>
        public long TotalEnqueued => Interlocked.Read(ref _totalEnqueued);

        /// <summary>
        /// 总丢弃数量
        /// </summary>
        public long TotalDropped => Interlocked.Read(ref _totalDropped);

        /// <summary>
        /// 注册的写入器数量
        /// </summary>
        public int WriterCount
        {
            get
            {
                _writersLock.EnterReadLock();
                try
                {
                    return _writers.Count;
                }
                finally
                {
                    _writersLock.ExitReadLock();
                }
            }
        }

        #endregion

        #region 构造函数

        /// <summary>
        /// 私有构造函数（单例模式）
        /// </summary>
        private VisionLogger()
        {
            _queue = new ConcurrentQueue<LogEntry>();
            _writers = new List<ILogWriter>();
            _writersLock = new ReaderWriterLockSlim();
            _cts = new CancellationTokenSource();
            _flushSignal = new SemaphoreSlim(0);

            // 启动后台处理任务
            _processingTask = Task.Run(ProcessQueueAsync);
        }

        /// <summary>
        /// 带默认来源的构造函数（用于 ForSource）
        /// </summary>
        private VisionLogger(VisionLogger parent, string defaultSource)
        {
            // 共享父实例的所有状态
            _queue = parent._queue;
            _writers = parent._writers;
            _writersLock = parent._writersLock;
            _cts = parent._cts;
            _flushSignal = parent._flushSignal;
            _processingTask = parent._processingTask;
            _minLevel = parent._minLevel;
            _defaultSource = defaultSource;
            
            // 不启动新的处理任务
            _processingTask = Task.CompletedTask;
        }

        #endregion

        #region ILogger 实现

        /// <summary>
        /// 记录日志（核心方法）- 高性能非阻塞
        /// </summary>
        public void Log(LogLevel level, string message, string? source = null, Exception? exception = null, LogTarget target = LogTarget.All)
        {
            if (!IsEnabled || level < _minLevel)
            {
                return;
            }

            // 检查队列容量
            if (_queue.Count >= MaxQueueSize)
            {
                _queue.TryDequeue(out _);
                Interlocked.Increment(ref _totalDropped);
            }

            var entry = LogEntry.Create(
                Interlocked.Increment(ref _idCounter),
                level,
                message,
                source ?? _defaultSource,
                exception,
                null,
                null,
                target
            );

            _queue.Enqueue(entry);
            Interlocked.Increment(ref _totalEnqueued);

            if (_queue.Count > QueueWarningThreshold)
            {
                // 队列即将溢出，记录警告
            }
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public void Info(string message, string? source = null, LogTarget target = LogTarget.All)
            => Log(LogLevel.Info, message, source, null, target);

        public void Success(string message, string? source = null, LogTarget target = LogTarget.All)
            => Log(LogLevel.Success, message, source, null, target);

        public void Warning(string message, string? source = null, LogTarget target = LogTarget.All)
            => Log(LogLevel.Warning, message, source, null, target);

        public void Error(string message, string? source = null, Exception? exception = null, LogTarget target = LogTarget.All)
            => Log(LogLevel.Error, message, source, exception, target);

        /// <summary>
        /// 创建带预设来源的日志器
        /// </summary>
        public ILogger ForSource(string source)
        {
            return new VisionLogger(this, source);
        }

        #endregion

        #region ILoggerProvider 实现

        public ILogger GetLogger() => this;

        public ILogger GetLogger(string source) => ForSource(source);

        public void AddWriter(ILogWriter writer)
        {
            _writersLock.EnterWriteLock();
            try
            {
                var existing = _writers.FirstOrDefault(w => w.Name == writer.Name);
                if (existing != null)
                {
                    _writers.Remove(existing);
                    existing.Dispose();
                }
                _writers.Add(writer);
            }
            finally
            {
                _writersLock.ExitWriteLock();
            }
        }

        public bool RemoveWriter(string name)
        {
            _writersLock.EnterWriteLock();
            try
            {
                var writer = _writers.FirstOrDefault(w => w.Name == name);
                if (writer != null)
                {
                    _writers.Remove(writer);
                    writer.Dispose();
                    return true;
                }
                return false;
            }
            finally
            {
                _writersLock.ExitWriteLock();
            }
        }

        public void SetMinLevel(LogLevel level)
        {
            _minLevel = level;
        }

        #endregion

        #region 后台处理

        /// <summary>
        /// 后台队列处理循环
        /// </summary>
        private async Task ProcessQueueAsync()
        {
            var batch = new List<LogEntry>(BatchSize);

            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    // 等待信号或超时
                    await _flushSignal.WaitAsync(BatchIntervalMs, _cts.Token).ContinueWith(t => { });

                    // 批量取出日志
                    batch.Clear();
                    while (batch.Count < BatchSize && _queue.TryDequeue(out var entry))
                    {
                        batch.Add(entry);
                    }

                    // 分发到写入器
                    if (batch.Count > 0)
                    {
                        DispatchToWriters(batch);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // 处理队列处理错误
                }
            }

            // 退出前处理剩余日志
            await FlushRemainingAsync();
        }

        /// <summary>
        /// 分发日志到所有写入器
        /// </summary>
        private void DispatchToWriters(IReadOnlyList<LogEntry> entries)
        {
            _writersLock.EnterReadLock();
            try
            {
                foreach (var writer in _writers)
                {
                    try
                    {
                        if (writer.IsEnabled)
                        {
                            var filtered = entries
                                .Where(e => e.Level >= writer.MinLevel)
                                .Where(e => writer.Accepts(e.Target))
                                .ToList();
                            if (filtered.Count > 0)
                            {
                                writer.WriteBatch(filtered);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // 写入器错误由写入器自己处理
                    }
                }
            }
            finally
            {
                _writersLock.ExitReadLock();
            }
        }

        /// <summary>
        /// 刷新剩余日志
        /// </summary>
        private async Task FlushRemainingAsync()
        {
            var remaining = new List<LogEntry>();
            while (_queue.TryDequeue(out var entry))
            {
                remaining.Add(entry);
            }

            if (remaining.Count > 0)
            {
                DispatchToWriters(remaining);
            }

            // 刷新所有写入器
            _writersLock.EnterReadLock();
            try
            {
                foreach (var writer in _writers)
                {
                    try
                    {
                        writer.Flush();
                    }
                    catch { }
                }
            }
            finally
            {
                _writersLock.ExitReadLock();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 立即刷新所有缓冲区
        /// </summary>
        public async Task FlushAsync()
        {
            _flushSignal.Release();
            await Task.Delay(10); // 给处理任务一点时间
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public (long Total, long Dropped, int QueueSize, int Writers) GetStatistics()
        {
            return (TotalEnqueued, TotalDropped, QueueSize, WriterCount);
        }

        /// <summary>
        /// 获取写入器列表
        /// </summary>
        public IReadOnlyList<string> GetWriterNames()
        {
            _writersLock.EnterReadLock();
            try
            {
                return _writers.Select(w => w.Name).ToList();
            }
            finally
            {
                _writersLock.ExitReadLock();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            // 请求取消
            _cts.Cancel();

            // 等待处理任务完成
            try
            {
                _processingTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch { }

            // 释放写入器
            _writersLock.EnterWriteLock();
            try
            {
                foreach (var writer in _writers)
                {
                    try
                    {
                        writer.Dispose();
                    }
                    catch { }
                }
                _writers.Clear();
            }
            finally
            {
                _writersLock.ExitWriteLock();
            }

            _cts.Dispose();
            _writersLock.Dispose();
            _flushSignal.Dispose();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
