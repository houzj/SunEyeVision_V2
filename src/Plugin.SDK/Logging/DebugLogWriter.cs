using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 调试输出日志写入器 - 输出到 Visual Studio 调试窗口
    /// </summary>
    /// <remarks>
    /// 用于开发调试阶段，仅在 DEBUG 模式下启用。
    /// </remarks>
    public class DebugLogWriter : ILogWriter
    {
        #region 属性

        public string Name => "DebugLogger";
        public string Description => "调试输出写入器";

        public LogTarget Channel => LogTarget.Debug;
        public LogLevel MinLevel { get; set; } = LogLevel.Info;
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 是否显示颜色标记
        /// </summary>
        public bool ShowColorMarkers { get; set; } = true;

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

#if DEBUG
            var message = FormatEntry(entry);
            Debug.WriteLine(message);
#endif
        }

        public void WriteBatch(IReadOnlyList<LogEntry> entries)
        {
            if (!IsEnabled || entries.Count == 0)
                return;

#if DEBUG
            var sb = new StringBuilder();
            foreach (var entry in entries)
            {
                if (entry.Level >= MinLevel)
                {
                    sb.AppendLine(FormatEntry(entry));
                }
            }
            Debug.Write(sb.ToString());
#endif
        }

        public void Flush()
        {
            // Debug 输出不需要刷新
        }

        public void Dispose()
        {
            // 无资源需要释放
        }

        #endregion

        #region 私有方法

        private string FormatEntry(LogEntry entry)
        {
            var levelMarker = ShowColorMarkers ? GetLevelMarker(entry.Level) : "";
            var sourceStr = ShowSource && !string.IsNullOrEmpty(entry.Source) ? $"[{entry.Source}] " : "";

            return $"{levelMarker}[{entry.FormattedTime}] [{entry.LevelText}] {sourceStr}{entry.Message}";
        }

        private static string GetLevelMarker(LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => "ℹ️ ",
                LogLevel.Success => "✅ ",
                LogLevel.Warning => "⚠️ ",
                LogLevel.Error => "❌ ",
                _ => ""
            };
        }

        #endregion
    }
}
