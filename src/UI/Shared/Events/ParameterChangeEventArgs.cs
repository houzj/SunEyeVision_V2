using System;

namespace SunEyeVision.UI.Shared.Events
{
    /// <summary>
    /// 参数变更类型
    /// </summary>
    public enum ParameterChangeType
    {
        /// <summary>常量值修改</summary>
        ConstantValueChanged,

        /// <summary>绑定类型切换</summary>
        BindingTypeChanged,

        /// <summary>动态绑定源配置</summary>
        DynamicBindingConfigured,

        /// <summary>重置为默认值</summary>
        ResetToDefault,

        /// <summary>批量应用绑定</summary>
        BatchApplied
    }

    /// <summary>
    /// 参数变更事件参数
    /// </summary>
    public class ParameterChangeEventArgs : EventArgs
    {
        /// <summary>参数名称</summary>
        public string ParameterName { get; init; } = string.Empty;

        /// <summary>参数显示名称</summary>
        public string? DisplayName { get; init; }

        /// <summary>变更类型</summary>
        public ParameterChangeType ChangeType { get; init; }

        /// <summary>旧值</summary>
        public object? OldValue { get; init; }

        /// <summary>新值</summary>
        public object? NewValue { get; init; }

        /// <summary>节点名称（可选）</summary>
        public string? NodeName { get; init; }

        /// <summary>节点ID（可选）</summary>
        public string? NodeId { get; init; }

        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <summary>额外信息</summary>
        public string? AdditionalInfo { get; init; }

        /// <summary>
        /// 创建常量值变更事件
        /// </summary>
        public static ParameterChangeEventArgs ConstantValueChanged(
            string paramName, string? displayName, object? oldValue, object? newValue,
            string? nodeName = null, string? nodeId = null)
        {
            return new ParameterChangeEventArgs
            {
                ParameterName = paramName,
                DisplayName = displayName,
                ChangeType = ParameterChangeType.ConstantValueChanged,
                OldValue = oldValue,
                NewValue = newValue,
                NodeName = nodeName,
                NodeId = nodeId
            };
        }

        /// <summary>
        /// 创建绑定类型变更事件
        /// </summary>
        public static ParameterChangeEventArgs BindingTypeChanged(
            string paramName, string? displayName, string oldType, string newType,
            string? nodeName = null, string? nodeId = null)
        {
            return new ParameterChangeEventArgs
            {
                ParameterName = paramName,
                DisplayName = displayName,
                ChangeType = ParameterChangeType.BindingTypeChanged,
                OldValue = oldType,
                NewValue = newType,
                NodeName = nodeName,
                NodeId = nodeId
            };
        }

        /// <summary>
        /// 创建动态绑定配置事件
        /// </summary>
        public static ParameterChangeEventArgs DynamicBindingConfigured(
            string paramName, string? displayName, string sourceNode, string sourceProperty,
            string? nodeName = null, string? nodeId = null)
        {
            return new ParameterChangeEventArgs
            {
                ParameterName = paramName,
                DisplayName = displayName,
                ChangeType = ParameterChangeType.DynamicBindingConfigured,
                NewValue = $"{sourceNode}.{sourceProperty}",
                NodeName = nodeName,
                NodeId = nodeId,
                AdditionalInfo = $"源节点: {sourceNode}, 源属性: {sourceProperty}"
            };
        }

        /// <summary>
        /// 创建重置事件
        /// </summary>
        public static ParameterChangeEventArgs ResetToDefault(
            string paramName, string? displayName, object? newValue,
            string? nodeName = null, string? nodeId = null)
        {
            return new ParameterChangeEventArgs
            {
                ParameterName = paramName,
                DisplayName = displayName,
                ChangeType = ParameterChangeType.ResetToDefault,
                NewValue = newValue,
                NodeName = nodeName,
                NodeId = nodeId
            };
        }

        /// <summary>
        /// 创建批量应用事件
        /// </summary>
        public static ParameterChangeEventArgs BatchApplied(
            string nodeName, int changeCount)
        {
            return new ParameterChangeEventArgs
            {
                ParameterName = "Batch",
                ChangeType = ParameterChangeType.BatchApplied,
                NewValue = changeCount,
                NodeName = nodeName,
                AdditionalInfo = $"共 {changeCount} 个参数"
            };
        }
    }
}
