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
    /// - 自动日志轮转（按日期）
    /// - 缓冲区优化
    /// - 线程安全
    /// </remarks>
    public class FileLogWriter : ILogWriter
    {
        #region 常量

        private const int BufferSize = 8192;
        private const long MaxFileSize = 10 * 1024 * 1024; // 10MB

        #endregion

        #region 私有字段

        private readonly string _logDirectory;
        private readonly object _lockObject = new();
        private StreamWriter? _writer;
        private string? _currentLogFile;
        private DateTime _currentLogFileDate;
        private long _currentFileSize;
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

        public FileLogWriter(string? logDirectory = null)
        {
            _logDirectory = logDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            EnsureLogDirectory();
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
                    _currentFileSize += Encoding.UTF8.GetByteCount(line);

                    // 检查文件大小
                    if (_currentFileSize > MaxFileSize)
                    {
                        RotateLogFile();
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[FileLogWriter] 写入错误: {ex.Message}");
#endif
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
                            _currentFileSize += Encoding.UTF8.GetByteCount(sb.ToString());
                        }
                    }

                    _writer?.Write(sb.ToString());

                    // 检查文件大小
                    if (_currentFileSize > MaxFileSize)
                    {
                        RotateLogFile();
                    }
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine($"[FileLogWriter] 批量写入错误: {ex.Message}");
#endif
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
                _writer?.Dispose();
                _writer = null;
            }
        }

        #endregion

        #region 私有方法

        private void EnsureLogDirectory()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        private void EnsureWriter()
        {
            var today = DateTime.Today;

            // 检查是否需要创建新文件（日期变化）
            if (_writer == null || _currentLogFileDate != today)
            {
                _writer?.Dispose();
                
                _currentLogFileDate = today;
                var dateStr = today.ToString("yyyyMMdd");
                var timeStr = DateTime.Now.ToString("HHmmss");
                _currentLogFile = Path.Combine(_logDirectory, $"SunEyeVision_{dateStr}_{timeStr}.log");
                _currentFileSize = 0;

                _writer = new StreamWriter(_currentLogFile, true, Encoding.UTF8, BufferSize)
                {
                    AutoFlush = false
                };

                // 写入文件头
                _writer.WriteLine($"=== SunEyeVision Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            }
        }

        private void RotateLogFile()
        {
            _writer?.Dispose();
            
            var timeStr = DateTime.Now.ToString("HHmmss");
            var dateStr = _currentLogFileDate.ToString("yyyyMMdd");
            _currentLogFile = Path.Combine(_logDirectory, $"SunEyeVision_{dateStr}_{timeStr}.log");
            _currentFileSize = 0;

            _writer = new StreamWriter(_currentLogFile, true, Encoding.UTF8, BufferSize)
            {
                AutoFlush = false
            };

            _writer.WriteLine($"=== Log Rotated at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
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
