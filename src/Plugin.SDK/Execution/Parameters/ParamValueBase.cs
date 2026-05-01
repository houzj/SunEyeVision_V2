using System;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 参数值包装器基类（非泛型）
    /// </summary>
    /// <remarks>
    /// 用于反射场景，无法确定泛型类型时使用。
    /// 提供非泛型的 ObjectValue 和 BindingConfig 访问。
    /// </remarks>
    public abstract class ParamValueBase : ObservableObject
    {
        /// <summary>
        /// 参数值（object 类型）
        /// </summary>
        /// <remarks>
        /// 泛型类中会被 new T Value 隐藏，通过此属性提供非泛型访问。
        /// </remarks>
        public abstract object? ObjectValue { get; set; }

        /// <summary>
        /// 绑定配置
        /// </summary>
        public abstract ParamSetting? BindingConfig { get; set; }

        /// <summary>
        /// 值来源标识
        /// </summary>
        public abstract string ValueSource { get; }

        /// <summary>
        /// 是否为绑定模式
        /// </summary>
        public abstract bool IsBinding { get; }

        /// <summary>
        /// 切换到绑定模式前保存的常量值（切回常量模式时恢复用）
        /// </summary>
        public abstract object? BindingConstantValue { get; set; }
    }
}
