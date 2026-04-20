namespace SunEyeVision.Plugin.SDK.Logging
{
    /// <summary>
    /// 插件日志器 - 提供全局静态日志访问入口
    /// </summary>
    /// <remarks>
    /// 此类为插件和工具提供便捷的日志访问方式，无需依赖注入。
    /// 应用启动时应通过 SetProvider 方法配置日志提供者。
    /// 
    /// 使用示例：
    /// <code>
    /// // 应用启动时配置
    /// PluginLogger.SetProvider(myLoggerProvider);
    /// 
    /// // 在工具中使用
    /// PluginLogger.Info("参数已更新", "阈值工具");
    /// PluginLogger.ParameterChanged("阈值", 100, 150, "阈值工具");
    /// </code>
    /// </remarks>
    public static class PluginLogger
    {
        private static ILoggerProvider? _provider;
        private static ILogger? _logger;

        /// <summary>
        /// 设置日志提供者（应用启动时调用）
        /// </summary>
        /// <param name="provider">日志提供者实例</param>
        public static void SetProvider(ILoggerProvider provider)
        {
            _provider = provider;
            _logger = provider?.GlobalLogger;
        }

        /// <summary>
        /// 获取日志器实例
        /// </summary>
        public static ILogger Logger => _logger ?? NullLogger.Instance;

        /// <summary>
        /// 检查是否已配置日志器
        /// </summary>
        public static bool IsConfigured => _logger != null;

        #region 便捷方法

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public static void Info(string message, string? source = null, LogTarget target = LogTarget.UI)
            => Logger.Info(message, source, target);

        public static void Success(string message, string? source = null, LogTarget target = LogTarget.UI)
            => Logger.Success(message, source, target);

        public static void Warning(string message, string? source = null, LogTarget target = LogTarget.UI)
            => Logger.Warning(message, source, target);

        public static void Error(string message, string? source = null, System.Exception? exception = null, LogTarget target = LogTarget.UI)
            => Logger.Error(message, source, exception, target);

        /// <summary>
        /// 记录参数变更日志
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="oldValue">旧值</param>
        /// <param name="newValue">新值</param>
        /// <param name="source">来源（节点名称）</param>
        public static void ParameterChanged(string paramName, object? oldValue, object? newValue, string? source = null)
        {
            var message = $"参数 [{paramName}] 值已修改: {FormatValue(oldValue)} → {FormatValue(newValue)}";
            Info(message, source);
        }

        /// <summary>
        /// 记录绑定模式变更日志
        /// </summary>
        public static void BindingModeChanged(string paramName, string oldMode, string newMode, string? source = null)
        {
            var message = $"参数 [{paramName}] 绑定模式变更: {oldMode} → {newMode}";
            Info(message, source);
        }

        /// <summary>
        /// 记录参数绑定日志（切换到绑定模式）
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="bindingSource">绑定源（格式：NodeName.PropertyName）</param>
        /// <param name="oldConstantValue">原常量值（可选）</param>
        /// <param name="source">来源（节点名称）</param>
        public static void ParameterBound(string paramName, string bindingSource, object? oldConstantValue = null, string? source = null)
        {
            var message = oldConstantValue != null
                ? $"参数 [{paramName}] 已绑定至 {bindingSource} (原常量值: {FormatValue(oldConstantValue)})"
                : $"参数 [{paramName}] 已绑定至 {bindingSource}";
            Info(message, source);
        }

        /// <summary>
        /// 记录参数解除绑定日志（切换回常量模式）
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="constantValue">当前常量值</param>
        /// <param name="oldBindingSource">原绑定源（可选）</param>
        /// <param name="source">来源（节点名称）</param>
        public static void ParameterUnbound(string paramName, object constantValue, string? oldBindingSource = null, string? source = null)
        {
            var message = oldBindingSource != null
                ? $"参数 [{paramName}] 已解除绑定，恢复为常量值: {FormatValue(constantValue)} (原绑定: {oldBindingSource})"
                : $"参数 [{paramName}] 已解除绑定，恢复为常量值: {FormatValue(constantValue)}";
            Info(message, source);
        }

        private static string FormatValue(object? value)
        {
            return value switch
            {
                null => "null",
                string s => $"\"{s}\"",
                double d => d.ToString("F2"),
                float f => f.ToString("F2"),
                _ => value.ToString() ?? "null"
            };
        }

        #endregion
    }

    /// <summary>
    /// 空日志器 - 当未配置日志提供者时使用
    /// </summary>
    internal class NullLogger : ILogger
    {
        public static readonly NullLogger Instance = new();

        public void Log(LogLevel level, string message, string? source = null, System.Exception? exception = null, LogTarget target = LogTarget.UI)
        {
            // 不执行任何操作
        }

        public ILogger ForSource(string source) => this;
    }
}
