using System;
using System.Collections.Generic;
using System.Text;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 控制台日志写入器 - 输出到控制台（带颜色）
    /// </summary>
    /// <remarks>
    /// 适用于控制台应用程序或服务模式运行。
    /// </remarks>
    public class ConsoleLogWriter : ILogWriter
    {
        #region 属性

        public string Name => "ConsoleLogger";
        public string Description => "控制台输出写入器";

        public LogTarget Channel => LogTarget.Console;
        public LogLevel MinLevel { get; set; } = LogLevel.Info;
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 是否使用颜色输出
        /// </summary>
        public bool UseColors { get; set; } = true;

        /// <summary>
        /// 是否显示来源
        /// </summary>
        public bool ShowSource { get; set; } = true;

        #endregion

        #region ILogWriter 实现

        public void Write(LogEntry entry)
        {
            if (!IsEnabled || entry.Level < MinLevel)
                return;

            try
            {
                WriteColored(entry);
            }
            catch
            {
                // 控制台可能不可用
            }
        }

        public void WriteBatch(IReadOnlyList<LogEntry> entries)
        {
            if (!IsEnabled || entries.Count == 0)
                return;

            try
            {
                foreach (var entry in entries)
                {
                    if (entry.Level >= MinLevel)
                    {
                        WriteColored(entry);
                    }
                }
            }
            catch
            {
                // 控制台可能不可用
            }
        }

        public void Flush()
        {
            try
            {
                Console.Out.Flush();
            }
            catch { }
        }

        public void Dispose()
        {
            // 无资源需要释放
        }

        #endregion

        #region 私有方法

        private void WriteColored(LogEntry entry)
        {
            var originalColor = Console.ForegroundColor;
            try
            {
                if (UseColors)
                {
                    Console.ForegroundColor = GetLevelColor(entry.Level);
                }

                var sourceStr = ShowSource && !string.IsNullOrEmpty(entry.Source) ? $"[{entry.Source}] " : "";
                var line = $"[{entry.FormattedTime}] [{entry.LevelText}] {sourceStr}{entry.Message}";

                Console.WriteLine(line);

                // 异常单独输出
                if (entry.Exception != null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  Exception: {entry.Exception.GetType().Name}: {entry.Exception.Message}");
                    if (!string.IsNullOrEmpty(entry.Exception.StackTrace))
                    {
                        Console.WriteLine($"  {entry.Exception.StackTrace}");
                    }
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        private static ConsoleColor GetLevelColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Success => ConsoleColor.Green,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
        }

        #endregion
    }
}
