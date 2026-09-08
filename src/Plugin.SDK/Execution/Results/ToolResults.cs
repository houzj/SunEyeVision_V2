using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    /// <summary>
    /// 属性映射条目（声明式属性元数据）
    /// </summary>
    /// <param name="PropertyName">属性英文名（对应代码中的属性名）</param>
    /// <param name="DisplayName">属性中文显示名（用于UI显示）</param>
    /// <param name="GroupName">分组名称（用于树形分组，null 表示无分组）</param>
    public record PropertyMapEntry(string PropertyName, string DisplayName, string? GroupName = null);

    /// <summary>
    /// 执行状态枚举
    /// </summary>
    public enum ExecutionStatus
    {
        /// <summary>
        /// 未执行
        /// </summary>
        NotExecuted = 0,

        /// <summary>
        /// 执行中
        /// </summary>
        Running = 1,

        /// <summary>
        /// 执行成功
        /// </summary>
        Success = 2,

        /// <summary>
        /// 执行失败
        /// </summary>
        Failed = 3,

        /// <summary>
        /// 执行超时
        /// </summary>
        Timeout = 4,

        /// <summary>
        /// 已取消
        /// </summary>
        Cancelled = 5,

        /// <summary>
        /// 部分成功
        /// </summary>
        PartialSuccess = 6
    }

    /// <summary>
    /// 结果项类型
    /// </summary>
    public enum ResultItemType
    {
        /// <summary>
        /// 数值
        /// </summary>
        Numeric,

        /// <summary>
        /// 文本
        /// </summary>
        Text,

        /// <summary>
        /// 布尔值
        /// </summary>
        Boolean,

        /// <summary>
        /// 几何形状
        /// </summary>
        Geometry,

        /// <summary>
        /// 图像
        /// </summary>
        Image,

        /// <summary>
        /// 数组/列表
        /// </summary>
        Array,

        /// <summary>
        /// 自定义对象
        /// </summary>
        Object
    }

    /// <summary>
    /// 结果项
    /// </summary>
    /// <remarks>
    /// 用于表示工具执行结果数据，支持多种数据类型和值的元数据。
    /// </remarks>
    public sealed class ResultItem
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 显示名称
        /// </summary>
        public string? DisplayName { get; set; }

        /// <summary>
        /// 值
        /// </summary>
        public object? Value { get; set; }

        /// <summary>
        /// 类型
        /// </summary>
        public ResultItemType Type { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        public string? Unit { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 是否通过
        /// </summary>
        public bool? IsPass { get; set; }

        /// <summary>
        /// 下限
        /// </summary>
        public double? LowerLimit { get; set; }

        /// <summary>
        /// 上限
        /// </summary>
        public double? UpperLimit { get; set; }

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// 创建数值项
        /// </summary>
        public static ResultItem Numeric(string name, double value, string? unit = null)
        {
            return new ResultItem
            {
                Name = name,
                Value = value,
                Type = ResultItemType.Numeric,
                Unit = unit
            };
        }

        /// <summary>
        /// 创建文本项
        /// </summary>
        public static ResultItem Text(string name, string value)
        {
            return new ResultItem
            {
                Name = name,
                Value = value,
                Type = ResultItemType.Text
            };
        }

        /// <summary>
        /// 创建布尔项
        /// </summary>
        public static ResultItem Boolean(string name, bool value)
        {
            return new ResultItem
            {
                Name = name,
                Value = value,
                Type = ResultItemType.Boolean,
                IsPass = value
            };
        }

        /// <summary>
        /// 获取数值（支持类型匹配）
        /// </summary>
        public double? GetNumericValue()
        {
            if (Type == ResultItemType.Numeric && Value is double d)
                return d;
            if (Value is IConvertible convertible)
                return convertible.ToDouble(null);
            return null;
        }

        /// <summary>
        /// 获取字符串值
        /// </summary>
        public string? GetStringValue()
        {
            return Value?.ToString();
        }
    }

    /// <summary>
    /// 工具执行结果基类
    /// </summary>
    /// <remarks>
    /// 所有工具执行结果的基类，提供统一的执行结果结构
    /// 
    /// 主要功能：
    /// 1. 统一的执行状态和错误信息
    /// 2. 支持可视化元素
    /// 3. 支持结果列表
    /// 4. 支持执行统计
    /// 
    /// 使用示例：
    /// <code>
    /// public class CircleFindResults : ToolResultsBase
    /// {
    ///     public Circle FoundCircle { get; set; }
    ///     public double Score { get; set; }
    ///     public PointD Center => FoundCircle.Center;
    ///     public double Radius => FoundCircle.Radius;
    ///     
    ///     public void BuildDisplayItems()
    ///     {
    ///         Display.Add(new DisplayItem
    ///         {
    ///             Name = "Circle", Type = DisplayItemType.Circle,
    ///             Geometries = new object[] { FoundCircle },
    ///             Color = 0xFF00FF00, LineWidth = 2
    ///         });
    ///         Display.Add(new DisplayItem
    ///         {
    ///             Name = "Center", Type = DisplayItemType.Point,
    ///             Geometries = new object[] { Center },
    ///             Color = 0xFFFF0000, LineWidth = 5
    ///         });
        ///     }
        /// }
    /// </code>
    /// </remarks>
    public abstract class ToolResults
    {
        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus Status { get; set; } = ExecutionStatus.NotExecuted;

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess => Status == ExecutionStatus.Success || Status == ExecutionStatus.PartialSuccess;

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 错误堆栈
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// 执行时间（毫秒）。double：保留亚毫秒精度（计时源为 Stopwatch.Elapsed.TotalMilliseconds）。
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 工具名称
        /// </summary>
        public string? ToolName { get; set; }

        /// <summary>
        /// 工具ID
        /// </summary>
        public string? ToolId { get; set; }

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// 检测区几何输出（ROI 契约，RoiShape[]，词目 "ROI"）。
        /// 工具 Run() 中由 ExtractInspectionGeometries() 填充用户绘制的检测区 RoiShape（几何值+样式），
        /// 进输出契约后，下游可四级绑定：整体 / [Type=X] 按形状 / [i] 单项 / [i].Geometry.属性。
        /// </summary>
        public RoiShape[] InspectionGeometries { get; set; } = Array.Empty<RoiShape>();

        /// <summary>
        /// 屏蔽区几何输出（ROI 契约，RoiShape[]，词目 "ROI"）。
        /// 工具 Run() 中由 ExtractMaskGeometries() 填充用户绘制的屏蔽区 RoiShape，
        /// 进输出契约后，下游可四级绑定（与 InspectionGeometries 一致）。
        /// </summary>
        public RoiShape[] MaskGeometries { get; set; } = Array.Empty<RoiShape>();

        /// <summary>
        /// 显示收集器（Push 模型）
        /// </summary>
        protected readonly DisplayCollector _display = new();

        /// <summary>显示项集合</summary>
        public DisplayCollector Display => _display;

        /// <summary>
        /// 获取所有可显示的图像（含过程图像和最终输出图像）
        /// </summary>
        /// <remarks>
        /// 子类重写此方法暴露所有图像输出，供 ImageControl 的 ComboBox 切换。
        /// 字典键使用属性名（如 "OutputImage", "BinaryImage", "InputImage"），
        /// 与 AvailableDataSource.PropertyName 匹配。
        /// </remarks>
        public virtual Dictionary<string, IVisionImage?> GetDisplayImages() => new();

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// 设置错误信息
        /// </summary>
        public void SetError(string message, Exception? exception = null)
        {
            Status = ExecutionStatus.Failed;
            ErrorMessage = message;
            if (exception != null)
            {
                ErrorStackTrace = exception.StackTrace;
            }
        }

        /// <summary>
        /// 设置成功
        /// </summary>
        public void SetSuccess(double executionTimeMs = 0)
        {
            Status = ExecutionStatus.Success;
            ExecutionTimeMs = executionTimeMs;
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static T CreateError<T>(string errorMessage, Exception? exception = null) where T : ToolResults, new()
        {
            var result = new T
            {
                Status = ExecutionStatus.Failed,
                ErrorMessage = errorMessage,
                Timestamp = DateTime.Now
            };
            if (exception != null)
            {
                result.ErrorStackTrace = exception.StackTrace;
            }
            return result;
        }

        /// <summary>
        /// 转换为字典形式数据
        /// </summary>
        public virtual Dictionary<string, object> ToDictionary()
        {
            var dict = new Dictionary<string, object>
            {
                ["Status"] = Status.ToString(),
                ["IsSuccess"] = IsSuccess,
                ["ExecutionTimeMs"] = ExecutionTimeMs,
                ["Timestamp"] = Timestamp
            };

            if (!string.IsNullOrEmpty(ErrorMessage))
                dict["ErrorMessage"] = ErrorMessage;

            if (!string.IsNullOrEmpty(ToolName))
                dict["ToolName"] = ToolName;

            // 使用反射获取所有结果项（统一使用新机制）
            var resultItems = GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !p.IsDefined(typeof(IgnoreBindAttribute)))
                .Where(p => p.CanRead)
                .Where(p => p.GetMethod?.IsPublic == true)
                .Where(p => p.PropertyType != typeof(Delegate))
                .Where(p => p.Name != "EqualityContract" && !p.Name.StartsWith("get_"))
                .Where(p => !typeof(ToolResults).IsAssignableFrom(p.PropertyType)) // 排除结果本身的属性
                .Select(p =>
                {
                    var value = p.GetValue(this);
                    if (value == null) return null;
                    return new { Name = p.Name, Value = value };
                })
                .Where(x => x != null);

            foreach (var item in resultItems)
            {
                if (item.Value != null)
                {
                    dict[item.Name] = item.Value;
                }
            }

            return dict;
        }

        #region 输出参数查询方法

        /// <summary>
        /// 获取所有输出参数属性
        /// </summary>
        public IEnumerable<PropertyInfo> GetOutputParameterProperties()
        {
            return GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => !p.IsDefined(typeof(IgnoreBindAttribute)));
        }

        /// <summary>
        /// 获取可绑定的输出参数属性（供下游节点绑定）
        /// </summary>
        public IEnumerable<PropertyInfo> GetBindableOutputProperties()
        {
            return GetOutputParameterProperties()
                .Where(p => !p.IsDefined(typeof(IgnoreBindAttribute)));
        }

        /// <summary>
        /// 获取输出参数值
        /// </summary>
        public T? GetOutputValue<T>(string propertyName)
        {
            var prop = GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return default;

            var value = prop.GetValue(this);
            if (value is T typedValue)
                return typedValue;

            return default;
        }

        /// <summary>
        /// 获取所有输出参数的名称和值
        /// </summary>
        public Dictionary<string, object?> GetOutputValues()
        {
            var result = new Dictionary<string, object?>();
            foreach (var prop in GetOutputParameterProperties())
            {
                result[prop.Name] = prop.GetValue(this);
            }
            return result;
        }

        /// <summary>
        /// 获取属性映射表（声明式属性元数据）
        /// </summary>
        /// <remarks>
        /// 返回值说明：
        /// - PropertyName: 属性英文名（必须与代码中的属性名一致）
        /// - DisplayName: 属性中文显示名（用于UI显示）
        /// - GroupName: 分组名称（用于树形分组，null 表示无分组）
        /// 
        /// 使用示例：
        /// <code>
        /// public override List&lt;PropertyMapEntry&gt; GetPropertyMap()
        /// {
        ///     return
        ///     [
        ///         new("OutputImage",          "输出图像",              null),
        ///         new("ThresholdUsed",        "实际使用的阈值",        "结果"),
        ///         new("MaxValueUsed",         "最大值",               "结果"),
        ///         new("ProcessedAt",          "处理时间戳",            "执行信息"),
        ///     ];
        /// }
        /// </code>
        /// </remarks>
        public virtual List<PropertyMapEntry> GetPropertyMap()
        {
            return new List<PropertyMapEntry>
            {
                new("ExecutionTimeMs", "执行耗时", "执行信息")
            };
        }

        /// <summary>
        /// 基于输出变量契约派生输出变量列表（SSOT）。
        /// 以声明性 GetPropertyMap() 为准，结合真实 CLR 属性类型与运行期值，
        /// 取代原 ToDictionary() 反射 + 各子类手写三虚方法对齐的模式。
        /// 变量类型复用 SdkTypeVocabulary 固定词汇；未映射类型或契约漏洞直接报错（rule-015）。
        /// </summary>
        public virtual IReadOnlyList<OutputVariable> GetOutputVariables()
        {
            var variables = new List<OutputVariable>();
            foreach (var entry in GetPropertyMap())
            {
                var prop = GetType().GetProperty(entry.PropertyName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null)
                {
                    throw new InvalidOperationException(
                        $"工具 '{ToolName ?? GetType().Name}' 的 GetPropertyMap 声明了输出属性 '{entry.PropertyName}'，但结果类型中不存在该属性（契约漏洞）。");
                }

                var runtimeValue = prop.GetValue(this);
                var variableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(prop.PropertyType)
                    ?? throw new InvalidOperationException(
                        $"工具 '{ToolName ?? GetType().Name}' 的输出属性 '{entry.PropertyName}' 类型 '{prop.PropertyType.Name}' 不是全局变量支持的通用类型，请检查 SdkTypeVocabulary 词汇表。");

                variables.Add(new OutputVariable
                {
                    Name = entry.PropertyName,
                    VariableType = variableType,
                    DisplayName = entry.DisplayName,
                    Group = entry.GroupName,
                    Kind = OutputVariableKind.Data,
                    RuntimeValue = OutputVariableArrayPromoter.Promote(runtimeValue, variableType, prop.PropertyType)
                });
            }

            // 框架级统一输出：检测区/屏蔽区几何（ROI 契约 RoiShape[]，词目 "ROI"，所有工具自动获得）。
            // 变量值即 RoiShape[]，绑定树按 [Type=X]/[i] 下钻，解析器经 [i].Geometry.属性 穿透。
            AddRoiContractVariable(variables, nameof(InspectionGeometries), "检测区图形", "输入区域");
            AddRoiContractVariable(variables, nameof(MaskGeometries), "屏蔽区图形", "输入区域");

            return variables;
        }

        /// <summary>
        /// 把检测区/屏蔽区几何（RoI 契约）追加为输出变量。
        /// 仅当结果类型实际存在该属性时追加（框架属性在所有 ToolResults 派生类均存在）。
        /// </summary>
        private void AddRoiContractVariable(List<OutputVariable> variables, string propertyName, string displayName, string group)
        {
            var prop = GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null) return;

            var runtimeValue = prop.GetValue(this);
            // 运行期该区域无值（null 或空数组，如引导式工具无 RoI 检测区，基类默认 Array.Empty）→ 不输出空行（"--"），避免"输入区域空白"观感；
            // 设计态结构另由 ToolRegistry + ResolveRoiDesignValue 提供
            if (runtimeValue == null) return;
            if (runtimeValue is Array arr && arr.Length == 0) return;
            var variableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(prop.PropertyType)
                ?? throw new InvalidOperationException(
                    $"工具 '{ToolName ?? GetType().Name}' 的 {propertyName} 类型 '{prop.PropertyType.Name}' 不是全局变量支持的通用类型，请检查 SdkTypeVocabulary 词汇表。");

            variables.Add(new OutputVariable
            {
                Name = propertyName,
                VariableType = variableType,
                DisplayName = displayName,
                Group = group,
                Kind = OutputVariableKind.Data,
                RuntimeValue = OutputVariableArrayPromoter.Promote(runtimeValue, variableType, prop.PropertyType)
            });
        }

        #endregion
    }



    /// <summary>
    /// 通用工具结果 - 用于动态承载结果项的具体实现
    /// </summary>
    public class GenericToolResults : ToolResults
    {
        private readonly List<ResultItem> _resultItems = new();

        /// <summary>
        /// 添加结果项
        /// </summary>
        public void AddResultItem(string name, object? value, ResultItemType type = ResultItemType.Object)
        {
            _resultItems.Add(new ResultItem
            {
                Name = name,
                Value = value,
                Type = type
            });
        }

        /// <summary>
        /// 基于输出变量契约派生：把动态 AddResultItem 塞入的结果项暴露为契约变量，
        /// 使子程序输入端口、全局变量注入等动态结果可被 OutputVariableResolver 按名寻址（SSOT）。
        /// 类型从运行时值推断并映射到 SdkTypeVocabulary 词汇表；未映射类型直接报错（rule-015）。
        /// </summary>
        public override IReadOnlyList<OutputVariable> GetOutputVariables()
        {
            var variables = new List<OutputVariable>();
            foreach (var item in _resultItems)
            {
                // 词汇表只注册接口类型（IVisionImage），运行时值可能是具体实现类（CvImage/HalImage），
                // 故图像类映射到接口类型以命中 "Image" 词汇。
                var runtimeType = item.Value is IVisionImage ? typeof(IVisionImage) : item.Value?.GetType();
                var variableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(runtimeType)
                    ?? throw new InvalidOperationException(
                        $"通用结果项 '{item.Name}' 的值类型 '{runtimeType?.Name ?? "null"}' 未注册到 SdkTypeVocabulary 词汇表。");

                variables.Add(new OutputVariable
                {
                    Name = item.Name,
                    VariableType = variableType,
                    DisplayName = item.DisplayName ?? item.Name,
                    Group = "结果",
                    Kind = OutputVariableKind.Data,
                    RuntimeValue = OutputVariableArrayPromoter.Promote(item.Value, variableType, runtimeType ?? typeof(object))
                });
            }
            return variables;
        }

        /// <summary>
        /// 从结果项中提取 Mat 图像供 UI 显示
        /// </summary>
        public override Dictionary<string, IVisionImage?> GetDisplayImages()
        {
            var images = new Dictionary<string, IVisionImage?>();
            foreach (var item in _resultItems)
            {
                if (item.Value is IVisionImage image && !image.IsEmpty)
                {
                    images[item.Name] = image;
                }
            }
            return images;
        }
    }
}
