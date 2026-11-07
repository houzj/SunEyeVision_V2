using System;
using System.IO;
using log4net;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;
// 只引入所需的异常类型，避免 log4net.Core.LogLevel 与本命名空间 LogLevel 冲突。
using LogException = log4net.Core.LogException;
using Microsoft.Extensions.Options;

namespace SunEyeVision.Foundation.Logging
{
    /// <summary>
    /// 基于 log4net 的日志服务实现，形态对齐 VisionMaster 原件的 <c>Log4netService</c>。
    /// </summary>
    /// <remarks>
    /// 与原件相同的部分：用 log4net 承担滚动、并发与格式化，支持按天/按大小滚动。
    /// 刻意修正的原件缺陷：
    /// 1) 原件在启动时回写 <c>log4net.config</c>，且用无参 <c>catch { }</c> 吞掉失败；本实现全部编程式配置，不读写外部配置文件。
    /// 2) 原件目录创建失败时静默继续；本实现让异常向上传播（遵循禁止静默回退）。
    /// 3) log4net 本身没有 TRACE 级别，原件把 Trace 映射到 Debug；本实现保留该映射并显式注明，不伪装成独立级别。
    /// </remarks>
    public sealed class Log4netLogService : ILogService
    {
        private const string REPOSITORY_NAME = "SunEyeVision";
        private const string LOGGER_NAME = "SunEyeVision";

        private readonly ILog _logger;

        /// <summary>构造日志服务并按配置完成 log4net 初始化。</summary>
        /// <param name="options">日志配置，未配置日志目录时直接抛异常。</param>
        public Log4netLogService(IOptions<LogOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);
            LogOptions value = options.Value;

            if (string.IsNullOrWhiteSpace(value.Directory))
            {
                throw new InvalidOperationException($"日志配置缺失：{LogOptions.SectionName}:Directory 未配置。");
            }

            Directory.CreateDirectory(value.Directory);

            RollingFileAppender appender = new()
            {
                File = Path.Combine(value.Directory, "suneye.log"),
                AppendToFile = true,
                RollingStyle = RollingFileAppender.RollingMode.Composite,
                DatePattern = "yyyy-MM-dd'.log'",
                StaticLogFileName = false,
                MaxSizeRollBackups = value.MaxRetainedFiles,
                MaximumFileSize = $"{Math.Max(1, value.MaxFileSize / (1024 * 1024))}MB",
                LockingModel = new FileAppender.MinimalLock(),
                Layout = new PatternLayout("%date{yyyy-MM-dd HH:mm:ss.fff} [%thread] %-5level - %message%newline")
            };
            appender.ActivateOptions();

            // log4net 自定义 repository 必须先创建：对未定义的名字直接 GetRepository 会抛
            // "Repository [x] is NOT defined"。先 CreateRepository，已存在（重复实例化）时取回现有实例，
            // 这样重复构造不会重复注册 appender。只捕获 log4net 明确的"已存在"语义异常，不吞其他失败。
            Hierarchy hierarchy;
            try
            {
                hierarchy = (Hierarchy)LogManager.CreateRepository(REPOSITORY_NAME);
            }
            catch (LogException)
            {
                hierarchy = (Hierarchy)LogManager.GetRepository(REPOSITORY_NAME);
            }
            hierarchy.Root.RemoveAllAppenders();
            hierarchy.Root.AddAppender(appender);
            hierarchy.Root.Level = hierarchy.LevelMap[ResolveLevelName(value.MinimumLevel)];
            hierarchy.Configured = true;

            _logger = LogManager.GetLogger(REPOSITORY_NAME, LOGGER_NAME);
        }

        /// <inheritdoc />
        public void Debug(params string[] messages) => Write(messages, _logger.Debug);

        /// <inheritdoc />
        public void Trace(params string[] messages) => Write(messages, _logger.Debug);

        /// <inheritdoc />
        public void Info(params string[] messages) => Write(messages, _logger.Info);

        /// <inheritdoc />
        public void Warn(params string[] messages) => Write(messages, _logger.Warn);

        /// <inheritdoc />
        public void Error(params string[] messages) => Write(messages, _logger.Error);

        /// <inheritdoc />
        public void Error(params Exception[] exceptions) => WriteExceptions(exceptions, _logger.Error);

        /// <inheritdoc />
        public void Fatal(params string[] messages) => Write(messages, _logger.Fatal);

        /// <inheritdoc />
        public void Fatal(params Exception[] exceptions) => WriteExceptions(exceptions, _logger.Fatal);

        private static void Write(string[] messages, Action<object> writer)
        {
            ArgumentNullException.ThrowIfNull(messages);
            foreach (string message in messages)
            {
                writer(message);
            }
        }

        private static void WriteExceptions(Exception[] exceptions, Action<object, Exception> writer)
        {
            ArgumentNullException.ThrowIfNull(exceptions);
            foreach (Exception exception in exceptions)
            {
                writer(exception.Message, exception);
            }
        }

        private static string ResolveLevelName(string? level)
        {
            string normalized = (level ?? "Info").Trim().ToUpperInvariant();
            return normalized switch
            {
                "DEBUG" => "DEBUG",
                "TRACE" => "DEBUG",
                "INFO" => "INFO",
                "WARN" => "WARN",
                "WARNING" => "WARN",
                "ERROR" => "ERROR",
                "FATAL" => "FATAL",
                _ => throw new InvalidOperationException($"不支持的日志级别：{level}")
            };
        }
    }
}
