using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 结构化日志条目
    /// </summary>
    public sealed class LogEntry
    {
        /// <summary>
        /// 日志唯一标识符（自增）
        /// </summary>
        public long Id { get; init; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; init; }

        /// <summary>
        /// 日志级别
        /// </summary>
        public LogLevel Level { get; init; }

        /// <summary>
        /// 日志消息
        /// </summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>
        /// 来源标识（节点名称、模块名、组件名等）
        /// </summary>
        public string? Source { get; init; }

        /// <summary>
        /// 追踪ID（用于关联同一执行链的日志）
        /// </summary>
        /// <remarks>
        /// 格式示例：wf_001_node_003（工作流ID_节点ID）
        /// 用于追踪一次完整执行过程中的所有相关日志。
        /// </remarks>
        public string? TraceId { get; init; }

        /// <summary>
        /// 异常信息
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// 日志目标通道（控制写入哪些写入器）
        /// </summary>
        public LogTarget Target { get; init; } = LogTarget.UI;

        /// <summary>
        /// 扩展属性（用于存储额外上下文信息）
        /// </summary>
        public Dictionary<string, object?>? Properties { get; init; }

        /// <summary>
        /// 格式化的时间字符串
        /// </summary>
        public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");

        /// <summary>
        /// 日志级别显示文本
        /// </summary>
        public string LevelText => Level.ToString().ToUpperInvariant();

        /// <summary>
        /// 日志级别矢量图标路径（用于WPF Path控件）
        /// </summary>
        public string LevelIconPath => Level switch
        {
            // 信息圆圈图标 - Info (蓝色)
            LogLevel.Info => "M8,0 C3.58,0 0,3.58 0,8 C0,12.42 3.58,16 8,16 C12.42,16 16,12.42 16,8 C16,3.58 12.42,0 8,0 Z M8.8,12 L7.2,12 L7.2,7.2 L8.8,7.2 L8.8,12 Z M8.8,6 L7.2,6 L7.2,4.4 L8.8,4.4 L8.8,6 Z",

            // 圆形对号图标 - Success (绿色)
            LogLevel.Success => "M8,0 C3.58,0 0,3.58 0,8 C0,12.42 3.58,16 8,16 C12.42,16 16,12.42 16,8 C16,3.58 12.42,0 8,0 Z M6.9,11.7 L3.2,8 L4.4,6.8 L6.9,9.3 L11.6,4.6 L12.8,5.8 L6.9,11.7 Z",

            // 三角形警告图标 - Warning (橙色)
            LogLevel.Warning => "M8,1 L15,14 L1,14 L8,1 Z M7.2,6 L7.2,10 L8.8,10 L8.8,6 L7.2,6 Z M7.2,11 L7.2,13 L8.8,13 L8.8,11 L7.2,11 Z",

            // 圆形叉号图标 - Error (红色)
            LogLevel.Error => "M8,0 C3.58,0 0,3.58 0,8 C0,12.42 3.58,16 8,16 C12.42,16 16,12.42 16,8 C16,3.58 12.42,0 8,0 Z M11.31,10.86 L10.86,11.31 L8,8.45 L5.14,11.31 L4.69,10.86 L7.55,8 L4.69,5.14 L5.14,4.69 L8,7.55 L10.86,4.69 L11.31,5.14 L8.45,8 L11.31,10.86 Z",

            _ => ""
        };

        /// <summary>
        /// 图标填充颜色
        /// </summary>
        public string LevelIconFill => Level switch
        {
            LogLevel.Info => "#2196F3",      // 蓝色
            LogLevel.Success => "#4CAF50",   // 绿色
            LogLevel.Warning => "#FF9800",   // 橙色
            LogLevel.Error => "#F44336",     // 红色
            _ => "#666666"
        };

        /// <summary>
        /// 图标背景色（圆形徽章背景）
        /// </summary>
        public string LevelIconBackground => Level switch
        {
            LogLevel.Info => "#E3F2FD",      // 浅蓝背景
            LogLevel.Success => "#E8F5E9",   // 浅绿背景
            LogLevel.Warning => "#FFF3E0",   // 浅橙背景
            LogLevel.Error => "#FFEBEE",     // 浅红背景
            _ => "#FFFFFF"
        };

        /// <summary>
        /// 显示名称（优先显示来源，否则显示"系统"）
        /// </summary>
        public string DisplayName => !string.IsNullOrEmpty(Source) ? Source : "系统";

        /// <summary>
        /// 一级分类（系统/运行/设备/UI）
        /// </summary>
        public string Category => ParseCategory(Source);

        /// <summary>
        /// 二级分类（模块名/工作流名）
        /// </summary>
        public string SubCategory => ParseSubCategory(Source);

        /// <summary>
        /// 格式化显示文本（包含来源前缀）
        /// </summary>
        /// <remarks>
        /// 用于UI信息列显示，自动将来源格式化为 [来源] 前缀。
        /// </remarks>
        public string DisplayMessage => !string.IsNullOrEmpty(Source)
            ? $"[{Source}] {Message}"
            : Message;

        /// <summary>
        /// 解析一级分类
        /// </summary>
        private static string ParseCategory(string? source)
        {
            if (string.IsNullOrEmpty(source)) return "其他";
            var dotIndex = source.IndexOf('.');
            return dotIndex > 0 ? source.Substring(0, dotIndex) : source;
        }

        /// <summary>
        /// 解析二级分类
        /// </summary>
        private static string ParseSubCategory(string? source)
        {
            if (string.IsNullOrEmpty(source)) return "";
            var parts = source.Split('.');
            return parts.Length > 1 ? parts[1] : "";
        }

        /// <summary>
        /// 日志级别颜色（用于UI前景色）
        /// </summary>
        public string LevelColor => Level switch
        {
            LogLevel.Info => "#2196F3",
            LogLevel.Success => "#4CAF50",
            LogLevel.Warning => "#FF9800",
            LogLevel.Error => "#F44336",
            _ => "#333333"
        };

        /// <summary>
        /// 日志级别背景色（用于UI行高亮）
        /// </summary>
        public string LevelBackground => Level switch
        {
            LogLevel.Info => "#E3F2FD",
            LogLevel.Success => "#E8F5E9",
            LogLevel.Warning => "#FFF3E0",
            LogLevel.Error => "#FFEBEE",
            _ => "#FFFFFF"
        };

        /// <summary>
        /// 创建日志条目
        /// </summary>
        public static LogEntry Create(
            long id,
            LogLevel level,
            string message,
            string? source = null,
            Exception? exception = null,
            Dictionary<string, object?>? properties = null,
            string? traceId = null,
            LogTarget target = LogTarget.UI)
        {
            return new LogEntry
            {
                Id = id,
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Source = source,
                Exception = exception,
                Properties = properties,
                TraceId = traceId,
                Target = target
            };
        }

        /// <summary>
        /// 格式化为字符串（用于文件输出）
        /// </summary>
        public string Format(bool includeException = true)
        {
            var levelStr = Level.ToString().ToUpperInvariant().PadRight(7);
            var timeStr = Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var sourceStr = !string.IsNullOrEmpty(Source) ? $"[{Source}] " : "";
            
            var result = $"[{timeStr}] [{levelStr}] {sourceStr}{Message}";
            
            if (includeException && Exception != null)
            {
                result += $"{Environment.NewLine}  Exception: {Exception.GetType().Name}: {Exception.Message}";
                if (!string.IsNullOrEmpty(Exception.StackTrace))
                {
                    result += $"{Environment.NewLine}  StackTrace: {Exception.StackTrace}";
                }
            }
            
            return result;
        }

        public override string ToString() => Format();
    }
}
