namespace SunEyeVision.Foundation.Logging
{
    /// <summary>
    /// 日志提供者接口 - 用于获取日志器实例
    /// </summary>
    /// <remarks>
    /// 实现依赖注入模式，支持在不同上下文中获取日志器。
    /// 通常由应用启动时配置，插件可通过此接口获取日志器。
    /// </remarks>
    public interface ILoggerProvider
    {
        /// <summary>
        /// 获取默认日志器
        /// </summary>
        ILogger GetLogger();

        /// <summary>
        /// 获取指定来源的日志器
        /// </summary>
        /// <param name="source">来源标识（插件名、模块名等）</param>
        ILogger GetLogger(string source);

        /// <summary>
        /// 获取全局日志器（静态访问入口）
        /// </summary>
        ILogger GlobalLogger { get; }

        /// <summary>
        /// 添加日志写入器
        /// </summary>
        void AddWriter(ILogWriter writer);

        /// <summary>
        /// 移除日志写入器
        /// </summary>
        bool RemoveWriter(string name);

        /// <summary>
        /// 设置最低日志级别
        /// </summary>
        void SetMinLevel(LogLevel level);
    }
}
