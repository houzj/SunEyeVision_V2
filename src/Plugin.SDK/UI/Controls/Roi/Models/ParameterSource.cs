using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models
{
    /// <summary>
    /// 参数源基类 - 用于参数绑定
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ConstantSource), "Constant")]
    [JsonDerivedType(typeof(NodeOutputSource), "NodeOutput")]
    public abstract class ParameterSource
    {
        /// <summary>
        /// 绑定类型
        /// </summary>
        public BindingType BindingType { get; protected set; }

        /// <summary>
        /// 数据类型（用于验证）
        /// </summary>
        public string DataType { get; set; } = "double";

        /// <summary>
        /// 获取参数值
        /// </summary>
        public abstract object? GetValue(IParameterContext? context, ILogger? logger = null);

        /// <summary>
        /// 克隆
        /// </summary>
        public abstract ParameterSource Clone();
    }

    /// <summary>
    /// 常量值源
    /// </summary>
    public class ConstantSource : ParameterSource
    {
        /// <summary>
        /// 常量值
        /// </summary>
        public object? Value { get; set; }

        public ConstantSource()
        {
            BindingType = BindingType.Constant;
        }

        public ConstantSource(object? value, string dataType = "double")
        {
            BindingType = BindingType.Constant;
            Value = value;
            DataType = dataType;
        }

        public override object? GetValue(IParameterContext? context, ILogger? logger = null)
        {
            logger?.LogInfo($"参数源获取：常量值，值={Value}，类型={Value?.GetType().Name ?? "null"}", "ParameterSource");
            return Value;
        }

        public override ParameterSource Clone()
        {
            return new ConstantSource(Value, DataType);
        }
    }

    /// <summary>
    /// 节点输出源 - 绑定到其他节点的输出
    /// </summary>
    public class NodeOutputSource : ParameterSource
    {
        /// <summary>
        /// 源节点ID
        /// </summary>
        public string NodeId { get; set; } = string.Empty;

        /// <summary>
        /// 输出端口名称
        /// </summary>
        public string OutputName { get; set; } = string.Empty;

        /// <summary>
        /// 输出索引（用于多值输出）
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// 属性路径（用于访问复合对象的属性，如 "Center.X"）
        /// </summary>
        public string? PropertyPath { get; set; }

        public NodeOutputSource()
        {
            BindingType = BindingType.NodeOutput;
        }

        public NodeOutputSource(string nodeId, string outputName, int? index = null, string? propertyPath = null)
        {
            BindingType = BindingType.NodeOutput;
            NodeId = nodeId;
            OutputName = outputName;
            Index = index;
            PropertyPath = propertyPath;
        }

        public override object? GetValue(IParameterContext? context, ILogger? logger = null)
        {
            logger?.LogInfo($"参数源获取：节点输出，节点ID={NodeId}，输出名={OutputName}，索引={Index}，属性路径={PropertyPath ?? "(无)"}", "ParameterSource");

            if (context == null || string.IsNullOrEmpty(NodeId) || string.IsNullOrEmpty(OutputName))
            {
                logger?.LogError($"参数源获取失败：上下文为空或节点信息不完整，节点ID={NodeId}，输出名={OutputName}", "ParameterSource");
                return null;
            }

            var value = context.GetNodeOutputValue(NodeId, OutputName, Index, PropertyPath);

            if (value == null)
            {
                logger?.LogWarning($"参数源获取警告：节点 {NodeId}.{OutputName} 返回null", "ParameterSource");
            }
            else
            {
                logger?.LogSuccess($"参数源获取成功：节点 {NodeId}.{OutputName}，值类型={value.GetType().Name}", "ParameterSource");
            }

            return value;
        }

        public override ParameterSource Clone()
        {
            return new NodeOutputSource(NodeId, OutputName, Index, PropertyPath)
            {
                DataType = DataType
            };
        }
    }

    /// <summary>
    /// 参数上下文接口 - 用于解析绑定值
    /// </summary>
    public interface IParameterContext
    {
        /// <summary>
        /// 获取节点输出值
        /// </summary>
        object? GetNodeOutputValue(string nodeId, string outputName, int? index = null, string? propertyPath = null);
    }
}
