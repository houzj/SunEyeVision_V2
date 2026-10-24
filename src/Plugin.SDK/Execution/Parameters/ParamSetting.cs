using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数设置模型
    /// </summary>
    /// <remarks>
    /// 定义单个参数的设置配置，支持常量值和动态绑定两种模式。
    /// 
    /// 核心概念：
    /// 1. 绑定类型（BindingType）：回答"值从哪里来"
    ///    - Constant: 固定常量值
    ///    - Binding: 从其他节点输出获取
    /// 
    /// 2. 数据类型（DataType）：回答"值是什么类型"
    ///    - 由 System.Type 定义，支持精确类型匹配
    ///    - 支持所有 .NET 基础类型和自定义类型
    /// 
    /// 使用示例：
    /// <code>
    /// // 创建常量设置
    /// var thresholdSetting = ParamSetting.CreateConstant("Threshold", 128);
    /// 
    /// // 创建动态绑定设置
    /// var radiusSetting = ParamSetting.CreateBinding("MinRadius", "circle_find_001", "Radius");
    /// </code>
    /// </remarks>
    public class ParamSetting
    {
        public string? SourceWorkflowId { get; set; }
        public Guid? SourceFieldId { get; set; }
        /// <summary>Versioned semantic reference. When present it is authoritative; name/path fields are display projections.</summary>
        public string? ReferenceExpression { get; set; }
        public SunEyeVision.Plugin.Contracts.BindingConversion Conversion { get; set; }
        /// <summary>
        /// 参数名称
        /// </summary>
        /// <remarks>
        /// 要设置的参数名称，对应 ToolParameters 类中的属性名。
        /// </remarks>
        public string ParameterName { get; set; } = string.Empty;

        /// <summary>
        /// 绑定类型
        /// </summary>
        /// <remarks>
        /// 只有两种基本绑定类型：Constant（常量）和 Binding（动态绑定）。
        /// </remarks>
        public BindingType BindingType { get; set; } = BindingType.Constant;

        /// <summary>
        /// 常量值
        /// </summary>
        /// <remarks>
        /// 当 BindingType 为 Constant 时使用此值。
        /// 可以是任意类型：int、double、string、bool、Point等。
        /// 数据类型由 ParameterMetadata.DataType 或 BindableParameter.DataType 决定。
        /// </remarks>
        public object? ConstantValue { get; set; }

        /// <summary>
        /// 源节点ID
        /// </summary>
        /// <remarks>
        /// 当 BindingType 为 Binding 时，指定数据来源的节点ID。
        /// </remarks>
        public string? SourceNodeId { get; set; }

        /// <summary>
        /// 源属性名称
        /// </summary>
        /// <remarks>
        /// 当 BindingType 为 Binding 时，指定从源节点结果的哪个属性获取值。
        /// 示例: "Radius", "Center.X", "CircleFound.Radius"
        /// </remarks>
        public string? SourceProperty { get; set; }

        /// <summary>
        /// 源节点名称（展示用）
        /// </summary>
        /// <remarks>
        /// 当 BindingType 为 Binding 时，源节点的显示名称。
        /// 如"图像载入1"、"图像阈值化4"。
        /// 用于加载后直接重建绑定显示文本，无需依赖 AvailableDataSources。
        /// </remarks>
        public string? SourceNodeName { get; set; }

        /// <summary>
        /// 源属性显示名称（展示用）
        /// </summary>
        /// <remarks>
        /// 当 BindingType 为 Binding 时，源属性的友好显示名称。
        /// 如"输出图像"、"实际使用的阈值"。
        /// </remarks>
        public string? SourceDisplayName { get; set; }

        /// <summary>
        /// 目标参数类型
        /// </summary>
        /// <remarks>
        /// 参数的目标类型，用于类型转换和验证。
        /// 类型信息由泛型参数 T 自动推断，无需序列化。
        /// </remarks>
        [JsonIgnore]
        public Type? TargetType { get; set; }

        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid => Validate().IsValid;

        /// <summary>
        /// 创建常量设置
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="value">常量值</param>
        /// <returns>参数设置实例</returns>
        public static ParamSetting CreateConstant(string parameterName, object? value)
        {
            return new ParamSetting
            {
                ParameterName = parameterName,
                BindingType = BindingType.Constant,
                ConstantValue = value,
                TargetType = value?.GetType()
            };
        }

        /// <summary>
        /// 创建动态绑定设置
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="sourceNodeId">源节点ID</param>
        /// <param name="sourceProperty">源属性名称</param>
        /// <returns>参数设置实例</returns>
        public static ParamSetting CreateBinding(
            string parameterName,
            string sourceNodeId,
            string sourceProperty)
        {
            return new ParamSetting
            {
                ParameterName = parameterName,
                BindingType = BindingType.Binding,
                SourceNodeId = sourceNodeId,
                SourceProperty = sourceProperty
            };
        }

        /// <summary>
        /// 验证设置配置
        /// </summary>
        /// <returns>验证结果</returns>
        public SettingValidationResult Validate()
        {
            var result = new SettingValidationResult { IsValid = true };

            if (string.IsNullOrWhiteSpace(ParameterName))
            {
                result.IsValid = false;
                result.Errors.Add("参数名称不能为空");
            }

            switch (BindingType)
            {
                case BindingType.Constant:
                    // 常量值可以为null，某些参数可能接受null
                    break;

                case BindingType.Binding:
                    if (ReferenceExpression is not null)
                    {
                        try { SunEyeVision.Plugin.Runtime.BindingReferenceSerializer.Deserialize(ReferenceExpression); }
                        catch (Exception fault) when (fault is System.Text.Json.JsonException or ArgumentException or InvalidOperationException or FormatException)
                        {
                            result.IsValid = false;
                            result.Errors.Add("动态绑定引用无效：" + fault.Message);
                        }
                        break;
                    }
                    if (string.IsNullOrWhiteSpace(SourceNodeId))
                    {
                        result.IsValid = false;
                        result.Errors.Add("动态绑定必须指定源节点ID");
                    }
                    if (string.IsNullOrWhiteSpace(SourceProperty))
                    {
                        result.IsValid = false;
                        result.Errors.Add("动态绑定必须指定源属性名称");
                    }
                    break;
            }

            return result;
        }

        /// <summary>
        /// 克隆当前设置
        /// </summary>
        /// <returns>克隆的设置实例</returns>
        public ParamSetting Clone()
        {
            return new ParamSetting
            {
                ParameterName = ParameterName,
                BindingType = BindingType,
                ConstantValue = ConstantValue,
                SourceNodeId = SourceNodeId,
                SourceWorkflowId = SourceWorkflowId, SourceFieldId = SourceFieldId,
                ReferenceExpression = ReferenceExpression, Conversion = Conversion,
                SourceProperty = SourceProperty,
                SourceNodeName = SourceNodeName,
                SourceDisplayName = SourceDisplayName,
                TargetType = TargetType
            };
        }

        /// <summary>
        /// 修复反序列化后 ConstantValue 为 JsonElement 的问题
        /// </summary>
        /// <param name="targetType">目标类型</param>
        /// <remarks>
        /// System.Text.Json 在反序列化 object? 类型时会将其解析为 JsonElement，
        /// 需要手动将其转换为目标类型。
        /// </remarks>
        internal void FixConstantValue(Type targetType)
        {
            if (ConstantValue is JsonElement jsonElement)
            {
                ConstantValue = JsonElementConverter.ConvertToType(jsonElement, targetType);
            }
        }

        /// <summary>
        /// 转换为字典（用于序列化）
        /// </summary>
        public Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["ParameterName"] = ParameterName,
                ["BindingType"] = (int)BindingType
            };
            if(ReferenceExpression!=null)dict["ReferenceExpression"]=ReferenceExpression;
            dict["Conversion"]=(int)Conversion;

            if (ConstantValue != null)
            {
                dict["ConstantValue"] = ConstantValue;
                dict["ConstantValueType"] = ConstantValue.GetType().AssemblyQualifiedName ?? ConstantValue.GetType().FullName!;
            }

            if (!string.IsNullOrEmpty(SourceNodeId))
                dict["SourceNodeId"] = SourceNodeId;

            if (!string.IsNullOrEmpty(SourceProperty))
                dict["SourceProperty"] = SourceProperty;

            if (!string.IsNullOrEmpty(SourceNodeName))
                dict["SourceNodeName"] = SourceNodeName;

            if (!string.IsNullOrEmpty(SourceDisplayName))
                dict["SourceDisplayName"] = SourceDisplayName;

            if (TargetType != null)
                dict["TargetType"] = TargetType.AssemblyQualifiedName ?? TargetType.FullName!;

            return dict;
        }

        /// <summary>
        /// 从字典创建设置（用于反序列化）
        /// </summary>
        public static ParamSetting FromDictionary(Dictionary<string, object> dict)
        {
            var setting = new ParamSetting
            {
                ParameterName = dict.TryGetValue("ParameterName", out var name) ? name?.ToString() ?? string.Empty : string.Empty
            };
            if(dict.TryGetValue("ReferenceExpression",out var reference))setting.ReferenceExpression=reference?.ToString();
            if(dict.TryGetValue("Conversion",out var conversion)&&int.TryParse(conversion?.ToString(),out var operation))setting.Conversion=(SunEyeVision.Plugin.Contracts.BindingConversion)operation;

            // ✅ 修复：BindingType 可能是 JsonElement，需要先转换为字符串
            if (dict.TryGetValue("BindingType", out var type))
            {
                var typeStr = type?.ToString();
                if (int.TryParse(typeStr, out int typeInt))
                {
                    setting.BindingType = (BindingType)typeInt;
                }
                else
                {
                    setting.BindingType = BindingType.Constant;
                }
            }
            else
            {
                setting.BindingType = BindingType.Constant;
            }

            if (dict.TryGetValue("ConstantValue", out var value) && value != null)
            {
                if (dict.TryGetValue("ConstantValueType", out var typeName))
                {
                    var typeNameStr = typeName?.ToString();
                    var valueType = string.IsNullOrEmpty(typeNameStr) ? null : Type.GetType(typeNameStr);
                    if (valueType != null && value.GetType() != valueType)
                    {
                        try
                        {
                            setting.ConstantValue = Convert.ChangeType(value, valueType);
                        }
                        catch
                        {
                            setting.ConstantValue = value;
                        }
                    }
                    else
                    {
                        setting.ConstantValue = value;
                    }
                }
                else
                {
                    setting.ConstantValue = value;
                }
            }

            if (dict.TryGetValue("SourceNodeId", out var nodeId))
                setting.SourceNodeId = nodeId?.ToString();

            if (dict.TryGetValue("SourceProperty", out var prop))
                setting.SourceProperty = prop?.ToString();

            if (dict.TryGetValue("SourceNodeName", out var nodeName))
                setting.SourceNodeName = nodeName?.ToString();

            if (dict.TryGetValue("SourceDisplayName", out var dispName))
                setting.SourceDisplayName = dispName?.ToString();

            if (dict.TryGetValue("TargetType", out var targetType))
            {
                var targetTypeStr = targetType?.ToString();
                if (!string.IsNullOrEmpty(targetTypeStr))
                {
                    setting.TargetType = Type.GetType(targetTypeStr);
                }
            }

            return setting;
        }

        /// <summary>
        /// 获取描述字符串
        /// </summary>
        public override string ToString()
        {
            return BindingType switch
            {
                BindingType.Constant => $"{ParameterName} = {ConstantValue ?? "null"}",
                BindingType.Binding => $"{ParameterName} <- {SourceNodeId}.{SourceProperty}",
                _ => $"{ParameterName} ({BindingType})"
            };
        }
    }

    /// <summary>
    /// 设置验证结果
    /// </summary>
    public class SettingValidationResult
    {
        /// <summary>
        /// 是否有效
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// 错误信息列表
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// JsonElement 转换器
    /// </summary>
    /// <remarks>
    /// 用于将 System.Text.Json 反序列化产生的 JsonElement 转换为实际类型。
    /// </remarks>
    internal static class JsonElementConverter
    {
        /// <summary>
        /// 将 JsonElement 转换为目标类型
        /// </summary>
        /// <param name="element">JsonElement 对象</param>
        /// <param name="targetType">目标类型</param>
        /// <returns>转换后的值</returns>
        public static object? ConvertToType(JsonElement element, Type targetType)
        {
            if (targetType == typeof(int))
                return element.GetInt32();
            if (targetType == typeof(double))
                return element.GetDouble();
            if (targetType == typeof(float))
                return element.GetSingle();
            if (targetType == typeof(bool))
                return element.GetBoolean();
            if (targetType == typeof(string))
                return element.GetString();
            if (targetType == typeof(short))
                return element.GetInt16();
            if (targetType == typeof(long))
                return element.GetInt64();
            if (targetType == typeof(ushort))
                return element.GetUInt16();
            if (targetType == typeof(uint))
                return element.GetUInt32();
            if (targetType == typeof(ulong))
                return element.GetUInt64();
            if (targetType == typeof(byte))
                return element.GetByte();
            if (targetType == typeof(sbyte))
                return element.GetSByte();
            if (targetType == typeof(decimal))
                return element.GetDecimal();
            if (targetType.IsEnum)
                return Enum.ToObject(targetType, element.GetInt32());

            // 复合类型（Point2d 等几何结构，非 IConvertible）：按目标类型直接 JSON 反序列化。
            // 原实现仅走 Convert.ChangeType，对非 IConvertible 类型必然失败并静默返回 null，
            // 导致 ParamValue<Point2d> 一类结构常量"保存 → 重开"后丢失（rule-015 禁止静默回退）。
            if (targetType != typeof(object) && !typeof(IConvertible).IsAssignableFrom(targetType))
            {
                return JsonSerializer.Deserialize(element.GetRawText(), targetType);
            }

            // 尝试通用转换
            try
            {
                return Convert.ChangeType(element.ToString(), targetType);
            }
            catch
            {
                return null;
            }
        }
    }
}
