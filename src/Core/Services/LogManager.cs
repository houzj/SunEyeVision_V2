using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.Core.Services
{
    /// <summary>
    /// 日志管理器 - 全局单例，统一管理日志
    /// </summary>
    /// <remarks>
    /// 此类保留作为向后兼容的静态入口点。
    /// 推荐使用 VisionLogger.Instance 直接访问。
    /// </remarks>
    public static class LogManager
    {
        /// <summary>
        /// 获取日志实例
        /// </summary>
        public static ILogger Instance => VisionLogger.Instance;

        /// <summary>
        /// 获取 VisionLogger 实例（提供更多功能）
        /// </summary>
        public static VisionLogger VisionLoggerInstance => VisionLogger.Instance;

        /// <summary>
        /// 设置日志级别
        /// </summary>
        public static void SetLogLevel(LogLevel level)
        {
            VisionLogger.Instance.MinLevel = level;
        }

        /// <summary>
        /// 添加日志写入器
        /// </summary>
        public static void AddWriter(ILogWriter writer)
        {
            VisionLogger.Instance.AddWriter(writer);
        }

        /// <summary>
        /// 移除日志写入器
        /// </summary>
        public static bool RemoveWriter(string name)
        {
            return VisionLogger.Instance.RemoveWriter(name);
        }

        /// <summary>
        /// 立即刷新所有缓冲区
        /// </summary>
        public static void Flush()
        {
            VisionLogger.Instance.FlushAsync().Wait();
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public static (long Total, long Dropped, int QueueSize, int Writers) GetStatistics()
        {
            return VisionLogger.Instance.GetStatistics();
        }
    }
}
