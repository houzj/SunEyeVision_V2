using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 文件日志写入器 - 高性能文件持久化
    /// </summary>
    /// <remarks>
    /// 特性：
    /// - 异步批量写入
    /// - 按 年/月/日 目录分层，文件名带时分秒
    /// - 缓冲区优化
    /// - 线程安全
    /// </remarks>
    public class FileLogWriter : ILogWriter
    {
        #region 常量

        private const int BufferSize = 8192;

        /// <summary>
        /// 自动落盘间隔（毫秒）：日志写入后距上次落盘超过该间隔则强制 Flush，
        /// 保证运行中日志可实时观测、且进程异常退出时不丢失缓冲日志。
        /// </summary>
        private const int AutoFlushIntervalMs = 2000;

        #endregion

        #region 私有字段

        private readonly string _logRootDirectory;
        private readonly object _lockObject = new();
        private StreamWriter? _writer;
        private string? _currentLogFile;
        private DateTime _currentLogFileDate;
        private DateTime _lastFlushTime = DateTime.MinValue;
        private bool _isDisposed;

        #endregion

        #region 属性

        public string Name => "FileLogger";
        public string Description => "文件日志写入器";

        public LogTarget Channel => LogTarget.File;
        public LogLevel MinLevel { get; set; } = LogLevel.Info;
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string? CurrentLogFile => _currentLogFile;

        /// <summary>
        /// 是否包含异常堆栈
        /// </summary>
        public bool IncludeStackTrace { get; set; } = true;

        #endregion

        #region 构造函数

        public FileLogWriter(string? logRootDirectory = null)
        {
            _logRootDirectory = logRootDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        }

        #endregion

        #region ILogWriter 实现

        public void Write(LogEntry entry)
        {
            if (!IsEnabled || entry.Level < MinLevel)
                return;

            lock (_lockObject)
            {
                try
                {
                    EnsureWriter();

                    var line = FormatEntry(entry);
                    _writer?.WriteLine(line);
                    TryAutoFlush();
                }
                catch
                {
                    // 文件写入失败不阻断日志队列（写入器内部异常不影响主流程）
                }
            }
        }

        public void WriteBatch(IReadOnlyList<LogEntry> entries)
        {
            if (!IsEnabled || entries.Count == 0)
                return;

            lock (_lockObject)
            {
                try
                {
                    EnsureWriter();

                    var sb = new StringBuilder();
                    foreach (var entry in entries)
                    {
                        if (entry.Level >= MinLevel)
                        {
                            sb.AppendLine(FormatEntry(entry));
                        }
                    }

                    _writer?.Write(sb.ToString());
                    TryAutoFlush();
                }
                catch
                {
                    // 文件写入失败不阻断日志队列（写入器内部异常不影响主流程）
                }
            }
        }

        public void Flush()
        {
            lock (_lockObject)
            {
                _writer?.Flush();
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            lock (_lockObject)
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
            }
        }

        /// <summary>
        /// 距上次落盘超过 AutoFlushIntervalMs 时强制 Flush，保证日志实时可观测。
        /// 必须在 _lockObject 锁内调用。
        /// </summary>
        private void TryAutoFlush()
        {
            var now = DateTime.Now;
            if ((now - _lastFlushTime).TotalMilliseconds >= AutoFlushIntervalMs)
            {
                _lastFlushTime = now;
                _writer?.Flush();
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 按 年/月/日 构建日志子目录：logs/{yyyy}/{MM}/{dd}
        /// </summary>
        private string GetLogDirectory(DateTime date)
        {
            var dir = Path.Combine(
                _logRootDirectory,
                date.ToString("yyyy"),
                date.ToString("MM"),
                date.ToString("dd"));
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void EnsureWriter()
        {
            var today = DateTime.Today;

            // 日期变化时切换到新的 年/月/日 子目录
            if (_writer == null || _currentLogFileDate != today)
            {
                _writer?.Dispose();

                _currentLogFileDate = today;
                var dir = GetLogDirectory(today);
                var timeStr = DateTime.Now.ToString("HHmmss");
                _currentLogFile = Path.Combine(dir, $"SunEyeVision_{timeStr}.txt");

                _writer = new StreamWriter(_currentLogFile, true, Encoding.UTF8, BufferSize)
                {
                    AutoFlush = false
                };

                // 写入文件头
                _writer.WriteLine($"=== SunEyeVision Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
        }

        private string FormatEntry(LogEntry entry)
        {
            var sb = new StringBuilder();
            
            // 时间戳和级别
            sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] ");
            sb.Append($"[{entry.LevelText}] ");

            // 来源
            if (!string.IsNullOrEmpty(entry.Source))
            {
                sb.Append($"[{entry.Source}] ");
            }

            // 消息
            sb.Append(entry.Message);

            // 异常
            if (IncludeStackTrace && entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append($"  Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");
                if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                {
                    sb.AppendLine();
                    sb.Append($"  StackTrace: {entry.Exception.StackTrace}");
                }
            }

            return sb.ToString();
        }

        #endregion
    }
}
