using System.Collections.Generic;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 日志写入器接口 - 支持多种输出目标
    /// </summary>
    /// <remarks>
    /// 实现此接口可将日志输出到不同目标：
    /// - 文件（FileLogWriter）
    /// - UI界面（UILogWriter）
    /// </remarks>
    public interface ILogWriter
    {
        /// <summary>
        /// 写入器名称（用于管理和识别）
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 写入器描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 支持的最低日志级别（低于此级别的日志将被过滤）
        /// </summary>
        LogLevel MinLevel { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        bool IsEnabled { get; set; }

        /// <summary>
        /// 此写入器处理的日志通道
        /// </summary>
        LogTarget Channel { get; }

        /// <summary>
        /// 写入单条日志
        /// </summary>
        /// <param name="entry">日志条目</param>
        /// <remarks>
        /// 此方法由日志队列调度调用，实现应保证线程安全。
        /// 对于批量写入场景，建议重写 WriteBatch 方法以优化性能。
        /// </remarks>
        void Write(LogEntry entry);

        /// <summary>
        /// 批量写入日志（可选优化）
        /// </summary>
        /// <param name="entries">日志条目列表</param>
        /// <remarks>
        /// 默认实现循环调用 Write 方法。
        /// 对于支持批量操作的写入器（如文件写入），应重写此方法以提高性能。
        /// </remarks>
        void WriteBatch(IReadOnlyList<LogEntry> entries)
        {
            foreach (var entry in entries)
            {
                Write(entry);
            }
        }

        /// <summary>
        /// 刷新缓冲区（用于文件等需要持久化的写入器）
        /// </summary>
        void Flush() { }

        /// <summary>
        /// 判断此写入器是否接受指定目标的日志
        /// </summary>
        bool Accepts(LogTarget target)
        {
            if (target == LogTarget.None) return false;
            if (target == LogTarget.All) return true;
            return (Channel & target) != 0;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        void Dispose() { }
    }
}
