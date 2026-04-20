using System;

namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 日志器扩展方法 - 提供语义化的日志记录API
    /// </summary>
    /// <remarks>
    /// 使用扩展方法可以更清晰地表达日志来源和上下文。
    /// 示例：
    /// <code>
    /// logger.NodeInfo("图像采集1", "开始采集图像");
    /// logger.NodeSuccess("图像采集1", "采集完成");
    /// logger.EngineInfo("工作流执行开始");
    /// logger.DeviceError("相机1", "连接失败", ex);
    /// </code>
    /// </remarks>
    public static class LoggerExtensions
    {
        #region 节点日志扩展

        /// <summary>
        /// 记录节点信息日志
        /// </summary>
        /// <param name="logger">日志器</param>
        /// <param name="nodeName">节点名称</param>
        /// <param name="message">日志消息</param>
        public static void NodeInfo(this ILogger logger, string nodeName, string message, LogTarget target = LogTarget.UI)
            => logger.Info(message, nodeName, target);

        public static void NodeSuccess(this ILogger logger, string nodeName, string message, LogTarget target = LogTarget.UI)
            => logger.Success(message, nodeName, target);

        public static void NodeWarning(this ILogger logger, string nodeName, string message, LogTarget target = LogTarget.UI)
            => logger.Warning(message, nodeName, target);

        public static void NodeError(this ILogger logger, string nodeName, string message, Exception? ex = null, LogTarget target = LogTarget.UI)
            => logger.Error(message, nodeName, ex, target);

        #endregion

        #region 工作流引擎日志扩展

        /// <summary>
        /// 记录工作流引擎信息日志
        /// </summary>
        public static void EngineInfo(this ILogger logger, string message, LogTarget target = LogTarget.UI)
            => logger.Info(message, LogSources.WorkflowEngine, target);

        public static void EngineSuccess(this ILogger logger, string message, LogTarget target = LogTarget.UI)
            => logger.Success(message, LogSources.WorkflowEngine, target);

        public static void EngineWarning(this ILogger logger, string message, LogTarget target = LogTarget.UI)
            => logger.Warning(message, LogSources.WorkflowEngine, target);

        public static void EngineError(this ILogger logger, string message, Exception? ex = null, LogTarget target = LogTarget.UI)
            => logger.Error(message, LogSources.WorkflowEngine, ex, target);

        #endregion

        #region 设备日志扩展

        /// <summary>
        /// 记录设备信息日志
        /// </summary>
        public static void DeviceInfo(this ILogger logger, string deviceName, string message, LogTarget target = LogTarget.UI)
            => logger.Info(message, deviceName, target);

        public static void DeviceSuccess(this ILogger logger, string deviceName, string message, LogTarget target = LogTarget.UI)
            => logger.Success(message, deviceName, target);

        public static void DeviceWarning(this ILogger logger, string deviceName, string message, LogTarget target = LogTarget.UI)
            => logger.Warning(message, deviceName, target);

        public static void DeviceError(this ILogger logger, string deviceName, string message, Exception? ex = null, LogTarget target = LogTarget.UI)
            => logger.Error(message, deviceName, ex, target);

        #endregion

        #region 系统日志扩展

        /// <summary>
        /// 记录系统信息日志
        /// </summary>
        public static void SystemInfo(this ILogger logger, string message, LogTarget target = LogTarget.UI)
            => logger.Info(message, LogSources.System, target);

        public static void SystemSuccess(this ILogger logger, string message, LogTarget target = LogTarget.UI)
            => logger.Success(message, LogSources.System, target);

        public static void SystemWarning(this ILogger logger, string message, LogTarget target = LogTarget.UI)
            => logger.Warning(message, LogSources.System, target);

        public static void SystemError(this ILogger logger, string message, Exception? ex = null, LogTarget target = LogTarget.UI)
            => logger.Error(message, LogSources.System, ex, target);

        #endregion

        #region UI组件日志扩展

        /// <summary>
        /// 记录UI组件信息日志
        /// </summary>
        public static void UIInfo(this ILogger logger, string componentName, string message, LogTarget target = LogTarget.UI)
            => logger.Info(message, componentName, target);

        public static void UISuccess(this ILogger logger, string componentName, string message, LogTarget target = LogTarget.UI)
            => logger.Success(message, componentName, target);

        public static void UIWarning(this ILogger logger, string componentName, string message, LogTarget target = LogTarget.UI)
            => logger.Warning(message, componentName, target);

        public static void UIError(this ILogger logger, string componentName, string message, Exception? ex = null, LogTarget target = LogTarget.UI)
            => logger.Error(message, componentName, ex, target);

        #endregion

        #region 插件日志扩展

        /// <summary>
        /// 记录插件信息日志
        /// </summary>
        public static void PluginInfo(this ILogger logger, string pluginName, string message, LogTarget target = LogTarget.UI)
            => logger.Info(message, pluginName, target);

        public static void PluginSuccess(this ILogger logger, string pluginName, string message, LogTarget target = LogTarget.UI)
            => logger.Success(message, pluginName, target);

        public static void PluginWarning(this ILogger logger, string pluginName, string message, LogTarget target = LogTarget.UI)
            => logger.Warning(message, pluginName, target);

        public static void PluginError(this ILogger logger, string pluginName, string message, Exception? ex = null, LogTarget target = LogTarget.UI)
            => logger.Error(message, pluginName, ex, target);

        #endregion
    }
}
