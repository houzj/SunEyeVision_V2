using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Shared.Events;

namespace SunEyeVision.UI.Services.Logging
{
    /// <summary>
    /// 参数变更日志记录器实现
    /// </summary>
    public class ParameterChangeLogger : IParameterChangeLogger
    {
        private readonly ILogger _logger;

        public ParameterChangeLogger(ILogger logger)
        {
            _logger = logger ?? VisionLogger.Instance;
        }

        public void LogParameterChange(ParameterChangeEventArgs e)
        {
            var source = BuildLogSource(e);
            var message = BuildLogMessage(e);
            var level = DetermineLogLevel(e.ChangeType);

            _logger.Log(level, message, source);
        }

        public void LogBatchParameterChange(string nodeName, int changeCount)
        {
            _logger.Info(
                $"节点 [{nodeName}] 参数配置已更新，共 {changeCount} 个参数",
                LogSource.UIConfig);
        }

        private string BuildLogSource(ParameterChangeEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.NodeName))
            {
                return LogSource.UI("配置", e.NodeName);
            }
            return LogSource.UIConfig;
        }

        private string BuildLogMessage(ParameterChangeEventArgs e)
        {
            var paramName = e.DisplayName ?? e.ParameterName;

            return e.ChangeType switch
            {
                ParameterChangeType.ConstantValueChanged =>
                    $"参数 [{paramName}] 值已修改: {FormatValue(e.OldValue)} → {FormatValue(e.NewValue)}",

                ParameterChangeType.BindingTypeChanged =>
                    $"参数 [{paramName}] 绑定类型变更: {e.OldValue} → {e.NewValue}",

                ParameterChangeType.DynamicBindingConfigured =>
                    $"参数 [{paramName}] 配置动态绑定: {e.NewValue}",

                ParameterChangeType.ResetToDefault =>
                    $"参数 [{paramName}] 已重置为默认值: {FormatValue(e.NewValue)}",

                ParameterChangeType.BatchApplied =>
                    $"批量应用参数绑定: {e.AdditionalInfo}",

                _ => $"参数 [{paramName}] 已变更"
            };
        }

        private LogLevel DetermineLogLevel(ParameterChangeType changeType)
        {
            return changeType switch
            {
                ParameterChangeType.ConstantValueChanged => LogLevel.Info,
                ParameterChangeType.BindingTypeChanged => LogLevel.Info,
                ParameterChangeType.DynamicBindingConfigured => LogLevel.Info,
                ParameterChangeType.ResetToDefault => LogLevel.Info,
                ParameterChangeType.BatchApplied => LogLevel.Success,
                _ => LogLevel.Info
            };
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
    }
}
