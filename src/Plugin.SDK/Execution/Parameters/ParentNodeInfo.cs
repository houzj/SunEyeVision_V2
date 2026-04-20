using System;
using System.Collections.Generic;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 父节点信息模型
    /// </summary>
    /// <remarks>
    /// 表示当前节点的一个父节点（上游节点）的完整信息。
    /// 用于参数绑定界面展示父节点列表及其输出属性。
    /// 
    /// 核心功能：
    /// 1. 父节点基本信息
    /// 2. 执行状态
    /// 3. 可绑定的输出属性列表
    /// 4. 最近一次执行结果
    /// 
    /// 使用示例：
    /// <code>
    /// // 获取父节点信息
    /// var parentNodes = dataQueryService.GetParentNodes(currentNodeId);
    /// 
    /// foreach (var parent in parentNodes)
    /// {
    ///     // 访问父节点信息: parent.NodeName, parent.NodeType, parent.ExecutionStatus
    ///
    ///     foreach (var prop in parent.OutputProperties)
    ///     {
    ///         // 访问输出属性: prop.DisplayName, prop.CurrentValue
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class ParentNodeInfo
    {
        /// <summary>
        /// 节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 节点名称
        /// </summary>
        public string NodeName { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; } = string.Empty;

        /// <summary>
        /// 节点类型显示名称
        /// </summary>
        public string? NodeTypeDisplayName { get; set; }

        /// <summary>
        /// 节点图标
        /// </summary>
        public string? NodeIcon { get; set; }

        /// <summary>
        /// 节点是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus ExecutionStatus { get; set; } = ExecutionStatus.NotExecuted;

        /// <summary>
        /// 是否已执行
        /// </summary>
        public bool HasExecuted => ExecutionStatus == ExecutionStatus.Success || ExecutionStatus == ExecutionStatus.PartialSuccess;

        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public long ExecutionTimeMs { get; set; }

        /// <summary>
        /// 执行时间戳
        /// </summary>
        public DateTime? ExecutionTimestamp { get; set; }

        /// <summary>
        /// 输出属性列表
        /// </summary>
        public List<AvailableDataSource> OutputProperties { get; set; } = new List<AvailableDataSource>();

        /// <summary>
        /// 输出属性数量
        /// </summary>
        public int OutputCount => OutputProperties.Count;

        /// <summary>
        /// 最近执行结果
        /// </summary>
        public ToolResults? LastResult { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 连接端口
        /// </summary>
        /// <remarks>
        /// 表示连接到当前节点的哪个端口。
        /// </remarks>
        public string? ConnectionPort { get; set; }

        /// <summary>
        /// 连接顺序
        /// </summary>
        /// <remarks>
        /// 在多父节点场景中，表示此父节点的连接顺序。
        /// </remarks>
        public int ConnectionOrder { get; set; }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => !string.IsNullOrEmpty(NodeId) && !string.IsNullOrEmpty(NodeName);

        /// <summary>
        /// 获取指定类型的输出属性
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <param name="allowDerivedTypes">是否允许派生类型</param>
        /// <returns>匹配的输出属性列表</returns>
        public List<AvailableDataSource> GetCompatibleProperties(Type targetType, bool allowDerivedTypes = true)
        {
            var result = new List<AvailableDataSource>();

            foreach (var prop in OutputProperties)
            {
                if (IsTypeCompatible(prop.PropertyType, targetType, allowDerivedTypes))
                {
                    prop.IsCompatible = true;
                    result.Add(prop);
                }
                else
                {
                    prop.IsCompatible = false;
                    prop.CompatibilityNote = $"类型不兼容：{prop.PropertyType.Name} -> {targetType.Name}";
                }
            }

            return result;
        }

        /// <summary>
        /// 检查类型兼容性
        /// </summary>
        private bool IsTypeCompatible(Type sourceType, Type targetType, bool allowDerivedTypes)
        {
            if (sourceType == targetType)
                return true;

            if (allowDerivedTypes)
            {
                // 检查是否可以转换
                if (targetType.IsAssignableFrom(sourceType))
                    return true;

                // 检查数值类型转换
                if (IsNumericType(sourceType) && IsNumericType(targetType))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 检查是否为数值类型
        /// </summary>
        private bool IsNumericType(Type type)
        {
            return type == typeof(int) ||
                   type == typeof(long) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(float) ||
                   type == typeof(double) ||
                   type == typeof(decimal);
        }

        /// <summary>
        /// 获取节点状态显示文本
        /// </summary>
        public string GetStatusText()
        {
            return ExecutionStatus switch
            {
                ExecutionStatus.NotExecuted => "未执行",
                ExecutionStatus.Running => "执行中...",
                ExecutionStatus.Success => "执行成功",
                ExecutionStatus.Failed => $"执行失败: {ErrorMessage}",
                ExecutionStatus.Timeout => "执行超时",
                ExecutionStatus.Cancelled => "已取消",
                ExecutionStatus.PartialSuccess => "部分成功",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 获取描述字符串
        /// </summary>
        public override string ToString()
        {
            return $"{NodeName} ({NodeType})";
        }

        /// <summary>
        /// 创建父节点信息
        /// </summary>
        public static ParentNodeInfo Create(
            string nodeId,
            string nodeName,
            string nodeType,
            ExecutionStatus status = ExecutionStatus.NotExecuted)
        {
            return new ParentNodeInfo
            {
                NodeId = nodeId,
                NodeName = nodeName,
                NodeType = nodeType,
                ExecutionStatus = status
            };
        }
    }

    /// <summary>
    /// 父节点信息扩展方法
    /// </summary>
    public static class ParentNodeInfoExtensions
    {
        /// <summary>
        /// 获取属性显示名称
        /// </summary>
        private static string GetDisplayName(string propertyName)
        {
            // 将 PascalCase 转换为可读的中文或英文
            // 示例: OutputImage -> "OutputImage" 或 "输出图像"
            return propertyName; // TODO: 可以添加本地化支持
        }

        /// <summary>
        /// 获取属性描述
        /// </summary>
        private static string? GetPropertyDescription(System.Reflection.PropertyInfo prop)
        {
            // 可以从特性中读取描述
            return null;
        }
    }
}
