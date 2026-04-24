using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数值包装器 - 统一管理参数值和绑定配置
    /// </summary>
    /// <remarks>
    /// 核心设计：每个参数属性存储一个 ParamValue，包含：
    /// 1. Value - 具体值（算法层使用）
    /// 2. BindingConfig - 绑定配置（框架层使用）
    /// 3. ValueSource - 值来源标识（用于调试）
    /// 
    /// 这样实现：
    /// - 算法层：parameters.Threshold.Value = 128（无感）
    /// - 框架层：parameters.Threshold.BindingConfig = ParamSetting（管理）
    /// - 序列化：一次序列化，同时保存值和配置
    /// </remarks>
    /// <typeparam name="T">参数类型</typeparam>
    public class ParamValue<T> : ParamValueBase
    {
        private T _value = default!;
        private ParamSetting? _bindingConfig;

        /// <summary>
        /// 参数值（算法层直接使用）
        /// </summary>
        /// <remarks>
        /// 无论常量模式还是绑定模式，Value 都存储具体值。
        /// 算法层只需要读取 Value，不需要关心来源。
        /// </remarks>
        public new T Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        /// <summary>
        /// 参数值（基类非泛型版本）
        /// </summary>
        [JsonIgnore]
        public override object? ObjectValue
        {
            get => _value;
            set
            {
                if (value == null)
                    SetProperty(ref _value, default!);
                else
                    SetProperty(ref _value, (T)value);
            }
        }

        /// <summary>
        /// 绑定配置（框架层管理）
        /// </summary>
        /// <remarks>
        /// 如果为 null，表示常量模式，Value 就是用户直接输入的常量。
        /// 如果不为 null，表示绑定模式，Value 是从绑定配置解析出来的值。
        ///
        /// 序列化时，BindingConfig 会一起保存。
        /// </remarks>
        public override ParamSetting? BindingConfig
        {
            get => _bindingConfig;
            set
            {
                if (SetProperty(ref _bindingConfig, value))
                {
                    // 反序列化后自动恢复 TargetType
                    if (_bindingConfig != null && _bindingConfig.TargetType == null)
                    {
                        _bindingConfig.TargetType = typeof(T);
                    }

                    // 修复反序列化后 ConstantValue 为 JsonElement 的问题
                    if (_bindingConfig != null)
                    {
                        _bindingConfig.FixConstantValue(typeof(T));
                    }

                    // 绑定配置变化时，标记值来源
                    OnPropertyChanged(nameof(ValueSource));
                    OnPropertyChanged(nameof(IsBinding));
                }
            }
        }

        /// <summary>
        /// 值来源标识（用于调试显示）
        /// </summary>
        [JsonIgnore]
        public override string ValueSource
        {
            get
            {
                if (BindingConfig == null)
                    return "常量";

                if (BindingConfig.BindingType == BindingType.Binding)
                    return $"绑定: {BindingConfig.SourceNodeId}.{BindingConfig.SourceProperty}";

                return "未知";
            }
        }

        /// <summary>
        /// 是否为绑定模式
        /// </summary>
        [JsonIgnore]
        public override bool IsBinding => BindingConfig != null && BindingConfig.BindingType == BindingType.Binding;

        /// <summary>
        /// 创建常量模式
        /// </summary>
        /// <param name="value">常量值</param>
        /// <param name="parameterName">参数名称</param>
        public static ParamValue<T> CreateConstant(T value, string parameterName)
        {
            return new ParamValue<T>
            {
                _value = value,
                _bindingConfig = new ParamSetting
                {
                    ParameterName = parameterName,
                    BindingType = BindingType.Constant,
                    ConstantValue = value,
                    TargetType = typeof(T)
                }
            };
        }

        /// <summary>
        /// 创建绑定模式
        /// </summary>
        /// <param name="parameterName">参数名称</param>
        /// <param name="sourceNodeId">源节点ID</param>
        /// <param name="sourceProperty">源属性名称</param>
        /// <param name="defaultValue">默认值</param>
        public static ParamValue<T> CreateBinding(
            string parameterName,
            string sourceNodeId,
            string sourceProperty,
            T defaultValue)
        {
            return new ParamValue<T>
            {
                _value = defaultValue,
                _bindingConfig = new ParamSetting
                {
                    ParameterName = parameterName,
                    BindingType = BindingType.Binding,
                    SourceNodeId = sourceNodeId,
                    SourceProperty = sourceProperty,
                    TargetType = typeof(T)
                }
            };
        }
    }
}
