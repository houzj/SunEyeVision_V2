using System;


namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 日志目标通道 - 支持位标记组合
    /// </summary>
    /// <remarks>
    /// 用于指定日志写入的目标位置。默认只写 UI 通道，
    /// 需要其他通道时显式使用位标记组合。
    /// 
    /// 使用示例：
    /// <code>
    /// // 只写UI（默认）
    /// LogInfo("消息");
    /// 
    /// // 写文件+UI
    /// LogError("异常", target: LogTarget.File | LogTarget.UI);
    /// 
    /// // 写全部通道
    /// LogInfo("重要", target: LogTarget.All);
    /// </code>
    /// </remarks>
    [Flags]
    public enum LogTarget
    {
        /// <summary>不写任何通道（用于临时静默）</summary>
        None = 0,

        /// <summary>UI日志面板</summary>
        UI = 1,

        /// <summary>磁盘日志文件</summary>
        File = 1 << 1,

        /// <summary>VS调试输出窗口</summary>
        Debug = 1 << 2,

        /// <summary>控制台输出</summary>
        Console = 1 << 3,

        /// <summary>所有通道</summary>
        All = UI | File | Debug | Console
    }
}
