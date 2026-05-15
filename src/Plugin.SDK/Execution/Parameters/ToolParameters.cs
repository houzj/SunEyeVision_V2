using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Validation;
using ValidationResult = SunEyeVision.Plugin.SDK.Validation.ValidationResult;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <remarks>
    /// 所有工具参数类的基类，提供参数验证、克隆、元数据功能和属性变更通知。
    /// 
    /// 设计理念：
    /// 1. 强类型参数，编译时检查
    /// 2. 自动验证支持，通过特性标注约束
    /// 3. 序列化友好，支持JSON/Binary
    /// 4. UI绑定友好，继承ObservableObject支持属性变更通知
    /// 5. 参数只定义一次，UI直接绑定Parameters属性
    /// 
    /// 多态序列化（双层机制）：
    /// 1. 基类特性：[JsonPolymorphic] 提供多态配置入口，[JsonDerivedType] 注册已知派生类
    /// 2. 动态注册：ParameterTypeRegistry 在运行时扫描并注册插件派生类型
    /// 
    /// 为什么需要双层机制：
    /// - 基类特性提供多态配置的入口点（必需）
    /// - 动态注册机制支持插件架构（基类编译时不知道插件派生类）
    /// - 两者配合才能完整支持可扩展的插件系统
    /// 
    /// 使用示例：
    /// <code>
    /// [JsonDerivedType(typeof(ThresholdParameters), "Threshold")]
    /// public class ThresholdParameters : ToolParameters
    /// {
    ///     private int _threshold = 128;
    ///     
    ///     [ParameterRange(0, 255)]
    ///     [ParameterDisplay(DisplayName = "阈值")]
    ///     public int Threshold
    ///     {
    ///         get => _threshold;
    ///         set => SetProperty(ref _threshold, value, "阈值");
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(GenericToolParameters), "Generic")]
    public abstract class ToolParameters : ObservableObject
    {
        /// <summary>
        /// 参数版本（用于序列化兼容性）
        /// </summary>
        [JsonPropertyOrder(99)]
        public int Version { get; set; } = 1;

        /// <summary>
        /// 执行上下文（由执行器注入）
        /// </summary>
        /// <remarks>
        /// 工具执行期间可用的运行时服务，如日志记录器。
        /// 此属性由 ToolExecutor 在执行前自动注入，工具开发者无需手动设置。
        /// 
        /// 使用扩展方法访问：
        /// <code>
        /// parameters.LogInfo("执行信息");
        /// parameters.LogWarning("警告信息");
        /// parameters.LogError("错误信息", exception);
        /// </code>
        /// </remarks>
        [JsonIgnore]
        public ExecutionContext? Context { get; internal set; }

        /// <summary>
        /// 设置执行上下文（供执行引擎调用，注入日志器等运行时服务）
        /// </summary>
        /// <param name="context">执行上下文</param>
        public void SetExecutionContext(ExecutionContext context)
        {
            Context = context;
        }

        /// <summary>
        /// 运行时解析的输入图像（由执行引擎在运行前设置）
        /// </summary>
        /// <remarks>
        /// 图像输入统一通过参数绑定框架管理。此属性由执行引擎在调用 Tool.Run() 之前设置，
        /// 工具通过 parameters.ResolvedInputImage 获取输入图像。
        /// 
        /// 图像来源优先级：
        /// 1. 参数绑定中的 ImageSource（AvailableDataSource）— 用户通过 ImageSourceSelector 选择
        /// 2. 引擎通过拓扑传递的默认图像 — 从父节点输出自动获取
        /// 
        /// 对于图像源节点（如 ImageLoad），此属性为 null，工具自行从文件加载图像。
        /// </remarks>
        [JsonIgnore]
        public HImage? ResolvedInputImage { get; set; }

        /// <summary>
        /// 验证所有参数
        /// </summary>
        /// <returns>验证结果</returns>
        public virtual ValidationResult Validate()
        {
            var result = new ValidationResult();
            // 子类重写此方法实现验证逻辑
            return result;
        }

        /// <summary>
        /// 从另一个参数对象复制值
        /// </summary>
        public virtual void CopyFrom(ToolParameters other)
        {
            if (other == null || other.GetType() != GetType())
                return;

            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                if (prop.CanRead && prop.CanWrite)
                {
                    prop.SetValue(this, prop.GetValue(other));
                }
            }
        }

        #region 参数分类查询方法

        /// <summary>
        /// 获取可绑定的输入参数属性
        /// </summary>
        public IEnumerable<PropertyInfo> GetBindableInputProperties()
        {
            return GetAllParameterProperties()
                .Where(p => !p.IsDefined(typeof(IgnoreBindAttribute)));
        }

        /// <summary>
        /// 获取可序列化的参数属性（排除标记[JsonIgnore]的）
        /// </summary>
        public IEnumerable<PropertyInfo> GetSaveableProperties()
        {
            return GetAllParameterProperties()
                .Where(p => !p.IsDefined(typeof(JsonIgnoreAttribute)));
        }

        /// <summary>
        /// 获取所有参数属性（排除Version）
        /// </summary>
        public IEnumerable<PropertyInfo> GetAllParameterProperties()
        {
            return GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.Name != nameof(Version));
        }

        #endregion

        /// <summary>
        /// 获取参数摘要（用于日志输出）
        /// </summary>
        /// <returns>参数值的简要描述</returns>
        public string GetParameterSummary()
        {
            var properties = GetSaveableProperties()
                .Where(p => p.Name != nameof(Version) && p.Name != nameof(Context))
                .Take(5)  // 最多显示5个参数
                .ToList();

            if (!properties.Any())
                return "(无参数)";

            var parts = new System.Collections.Generic.List<string>();
            foreach (var prop in properties)
            {
                var value = prop.GetValue(this);
                var displayName = prop.Name;
                
                // 简化显示
                var valueStr = value switch
                {
                    null => "null",
                    string s => s.Length > 20 ? s.Substring(0, 20) + "..." : s,
                    _ => value.ToString()
                };
                
                parts.Add($"{displayName}={valueStr}");
            }

            var result = string.Join(", ", parts);
            if (properties.Count < GetSaveableProperties().Count(p => p.Name != nameof(Version) && p.Name != nameof(Context)))
                result += " ...";

            return result;
        }

        #region 参数特性查询方法

        /// <summary>
        /// 判断参数是否需要保存到项目文件
        /// </summary>
        public bool ShouldSave(string propertyName)
        {
            var prop = GetType().GetProperty(propertyName);
            if (prop == null) return false;
            return !prop.IsDefined(typeof(JsonIgnoreAttribute));
        }

        /// <summary>
        /// 判断参数是否支持数据绑定
        /// </summary>
        public bool CanBind(string propertyName)
        {
            var prop = GetType().GetProperty(propertyName);
            if (prop == null) return false;

            // 没有IgnoreBind标记的参数都支持绑定
            return !prop.IsDefined(typeof(IgnoreBindAttribute));
        }

        #endregion
    }

    /// <summary>
    /// 通用工具参数 - 用于兼容层的具体实现
    /// </summary>
    public class GenericToolParameters : ToolParameters
    {
        private readonly Dictionary<string, object?> _values = new();

        /// <summary>
        /// 设置参数值
        /// </summary>
        public void SetValue(string name, object? value)
        {
            _values[name] = value;
        }

        /// <summary>
        /// 获取参数值
        /// </summary>
        public T? GetValue<T>(string name)
        {
            if (_values.TryGetValue(name, out var value))
            {
                if (value is T typedValue)
                    return typedValue;
                try
                {
                    return (T?)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return default;
                }
            }
            return default;
        }

        /// <summary>
        /// 获取所有参数名称
        /// </summary>
        public IEnumerable<string> GetParameterNames() => _values.Keys;
    }
}
