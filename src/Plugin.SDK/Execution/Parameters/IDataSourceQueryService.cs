using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 节点执行上下文
    /// </summary>
    /// <remarks>
    /// 统一封装节点信息和执行结果，用于设计时和运行时。
    /// 
    /// 核心功能：
    /// 1. 节点基本信息（ID、名称、类型、图标）
    /// 2. 执行状态和结果
    /// 3. 结果类型（用于设计时推断输出属性）
    /// 4. 运行时/设计时标识
    /// </remarks>
    public class NodeExecutionContext
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
        /// 节点图标
        /// </summary>
        public string? NodeIcon { get; set; }

        /// <summary>
        /// 执行结果（运行时）
        /// </summary>
        public ToolResults? Result { get; set; }

        /// <summary>
        /// 结果类型（设计时）
        /// </summary>
        public Type? ResultType { get; set; }

        /// <summary>
        /// 节点参数（用于动态输出契约 IOutputContractProvider 查询）
        /// </summary>
        public ToolParameters? Parameters { get; set; }

        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus ExecutionStatus => Result?.Status ?? ExecutionStatus.NotExecuted;

        /// <summary>
        /// 是否有执行结果
        /// </summary>
        public bool HasResult => Result != null;

        /// <summary>
        /// 是否为运行时
        /// </summary>
        public bool IsRuntime => Result != null;

        /// <summary>
        /// 是否为设计时
        /// </summary>
        public bool IsDesignTime => Result == null;
    }

    /// <summary>
    /// 数据源查询服务接口
    /// </summary>
    /// <remarks>
    /// 提供查询父节点及其输出属性的能力，用于参数绑定界面。
    /// 
    /// 核心功能：
    /// 1. 查询父节点列表
    /// 2. 查询可绑定数据源
    /// 3. 按类型过滤数据源
    /// 4. 获取节点执行上下文（统一设计时和运行时）
    /// 
    /// 使用示例：
    /// <code>
    /// // 获取数据源查询服务实例
    /// IDataSourceQueryService queryService = ...;
    /// 
    /// // 查询父节点
    /// var parentNodes = queryService.GetParentNodes(currentNodeId);
    /// 
    /// // 查询可绑定数据源（按类型过滤）
    /// var doubleDataSources = queryService.GetAvailableDataSources(currentNodeId, typeof(double));
    /// 
    /// // 获取节点执行上下文
    /// var context = queryService.GetNodeContext(nodeId);
    /// if (context.IsRuntime)
    /// {
    ///     // 运行时，有实际结果
    ///     var result = context.Result;
    /// }
    /// else
    /// {
    ///     // 设计时，只有类型信息
    ///     var resultType = context.ResultType;
    /// }
    /// </code>
    /// </remarks>
    public interface IDataSourceQueryService
    {
        /// <summary>
        /// 获取父节点列表
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <returns>父节点信息列表</returns>
        List<ParentNodeInfo> GetParentNodes(string nodeId);

        /// <summary>
        /// 获取直接下游节点列表（画布连接驱动）。
        /// 用于条件分支工具"目标节点"下拉：返回 Connection.SourceNodeId == 当前节点的所有 Target 节点。
        /// 与 GetParentNodes 对称。
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <returns>直接下游节点信息列表</returns>
        List<ParentNodeInfo> GetChildNodes(string nodeId);

        /// <summary>
        /// 获取可用数据源
        /// </summary>
        /// <param name="nodeId">当前节点ID</param>
        /// <param name="targetType">目标参数类型（可选，用于类型过滤）</param>
        /// <returns>可用数据源列表</returns>
        List<AvailableDataSource> GetAvailableDataSources(string nodeId, Type? targetType = null);


        /// <summary>
        /// 获取指定父节点的输出属性
        /// </summary>
        /// <param name="parentNodeId">父节点ID</param>
        /// <returns>输出属性列表</returns>
        List<AvailableDataSource> GetNodeOutputProperties(string parentNodeId);

        /// <summary>
        /// 获取当前工作流中所有节点的数据源（子程序输出/显示绑定用）
        /// </summary>
        /// <returns>所有节点的输出属性列表</returns>
        List<AvailableDataSource> GetWorkflowNodeDataSources();

        /// <summary>
        /// 获取节点执行上下文
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>节点执行上下文（如果存在）</returns>
        NodeExecutionContext? GetNodeContext(string nodeId);

        /// <summary>
        /// 获取属性值
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值（如果存在）</returns>
        object? GetPropertyValue(string nodeId, string propertyName);

        /// <summary>
        /// 检查节点是否已执行
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>是否已执行</returns>
        bool HasNodeExecuted(string nodeId);

        /// <summary>
        /// 获取当前节点ID
        /// </summary>
        string? CurrentNodeId { get; set; }

        /// <summary>
        /// 检查节点是否已注册
        /// </summary>
        /// <param name="nodeId">节点ID</param>
        /// <returns>是否已注册</returns>
        bool IsNodeRegistered(string nodeId);
    }

    /// <summary>
    /// 数据源查询服务扩展方法
    /// </summary>
    public static class DataSourceQueryServiceExtensions
    {
        /// <summary>
        /// 获取指定类型的可用数据源
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="service">数据源查询服务</param>
        /// <param name="nodeId">节点ID</param>
        /// <returns>可用数据源列表</returns>
        public static List<AvailableDataSource> GetAvailableDataSources<T>(this IDataSourceQueryService service, string nodeId)
        {
            return service.GetAvailableDataSources(nodeId, typeof(T));
        }

        /// <summary>
        /// 获取属性值并转换为指定类型
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="service">数据源查询服务</param>
        /// <param name="nodeId">节点ID</param>
        /// <param name="propertyName">属性名称</param>
        /// <returns>属性值</returns>
        public static T? GetPropertyValue<T>(this IDataSourceQueryService service, string nodeId, string propertyName)
        {
            var value = service.GetPropertyValue(nodeId, propertyName);
            if (value is T typedValue)
                return typedValue;

            if (value != null)
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }

            return default;
        }

        /// <summary>
        /// 检查绑定是否可用
        /// </summary>
        /// <param name="service">数据源查询服务</param>
        /// <param name="binding">参数绑定</param>
        /// <returns>是否可用</returns>
        public static bool IsBindingAvailable(this IDataSourceQueryService service, ParamSetting binding)
        {
            if (binding.BindingType != BindingType.Binding)
                return true;

            if (string.IsNullOrEmpty(binding.SourceNodeId) || string.IsNullOrEmpty(binding.SourceProperty))
                return false;

            return service.HasNodeExecuted(binding.SourceNodeId);
        }

        /// <summary>
        /// 验证绑定配置
        /// </summary>
        /// <param name="service">数据源查询服务</param>
        /// <param name="binding">参数绑定</param>
        /// <returns>验证结果</returns>
        public static SettingValidationResult ValidateBinding(this IDataSourceQueryService service, ParamSetting binding)
        {
            var result = binding.Validate();

            if (binding.BindingType == BindingType.Binding &&
                !string.IsNullOrEmpty(binding.SourceNodeId) &&
                !string.IsNullOrEmpty(binding.SourceProperty))
            {
                // 检查源节点是否存在
                if (!service.HasNodeExecuted(binding.SourceNodeId))
                {
                    result.Warnings.Add($"源节点 {binding.SourceNodeId} 尚未执行");
                }

                // 检查属性是否存在
                var properties = service.GetNodeOutputProperties(binding.SourceNodeId);
                var propertyExists = properties.Exists(p => p.PropertyName == binding.SourceProperty);
                if (!propertyExists)
                {
                    result.IsValid = false;
                    result.Errors.Add($"源节点 {binding.SourceNodeId} 不存在属性 {binding.SourceProperty}");
                }

                // 检查类型兼容性
                if (binding.TargetType != null)
                {
                    var prop = properties.Find(p => p.PropertyName == binding.SourceProperty);
                    if (prop != null && !IsTypeCompatible(prop.PropertyType, binding.TargetType))
                    {
                        result.Warnings.Add($"类型可能不兼容: {prop.PropertyType.Name} -> {binding.TargetType.Name}");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 检查类型兼容性
        /// </summary>
        private static bool IsTypeCompatible(Type sourceType, Type targetType)
        {
            if (sourceType == targetType)
                return true;

            if (targetType.IsAssignableFrom(sourceType))
                return true;

            // 数值类型互转
            if (IsNumericType(sourceType) && IsNumericType(targetType))
                return true;

            return false;
        }

        /// <summary>
        /// 检查是否为数值类型
        /// </summary>
        private static bool IsNumericType(Type type)
        {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(byte) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }
    }
}
