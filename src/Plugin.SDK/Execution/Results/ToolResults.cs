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
    /// <param name="IsGroupPlaceholder">
    /// 是否为"分组占位"条目：该条目只是所在分组的语义占位（如"引导几何"），**不渲染为中间节点**，
    /// 其展开的子节点（几何元素字段）直接挂到分组下（单层折叠）。
    /// </param>
    /// <remarks>
    /// ★ 声明式替代（方案 P0.6-5）：此前由 Shell（<c>MainWindowViewModel</c>）硬编码
    /// <c>e.Group == "输入/卡尺/引导线"</c> 判断 —— 那是"UI 认具体工具的中文分组名"，
    /// 任何工具改分组名、或新工具想复用折叠都会**静默失效**（结果树多一层重复节点且不报错）。
    /// 现由**声明方**（工具的 <c>GetPropertyMap</c>）决定，Shell 只读标志（rule-008 / rule-016）。
    /// </remarks>
    public record PropertyMapEntry(
        string PropertyName, string DisplayName, string? GroupName = null, bool IsGroupPlaceholder = false,
        bool DisplayInResultPanel = true);

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
        [System.Text.Json.Serialization.JsonIgnore]
        public SunEyeVision.Plugin.SDK.Core.ToolExecutionIdentity? ExecutionIdentity { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public SunEyeVision.Plugin.Contracts.ToolSchema? BindingOutputSchema { get; internal set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public long BindingSchemaRevision { get; internal set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public ResultResourceOwner? Resources { get; protected set; }
        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus Status { get; set; } = ExecutionStatus.NotExecuted;

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess => Status == ExecutionStatus.Success || Status == ExecutionStatus.PartialSuccess;

        /// <summary>
        /// 是否由取消请求结束
        /// </summary>
        public bool IsCancelled => Status == ExecutionStatus.Cancelled;

        /// <summary>
        /// 节点状态（本次执行的最终结论：执行成功且判定通过 = true）。
        /// 由基类统一下沉，所有工具在结果树"执行信息"分组恒定可见，杜绝各工具漏声明/命名不一；
        /// 判定类工具在 Run 中写入判定结果，执行失败时由 SetError/CreateError 自动置 false。
        /// </summary>
        public bool IsPass { get; set; } = true;

        /// <summary>
        /// 面向界面的执行状态文本。执行状态与业务判定分开，避免“算法执行成功但尺寸越界”被显示为执行失败。
        /// </summary>
        public string ExecutionStatusText => Status switch
        {
            ExecutionStatus.NotExecuted => "未执行",
            ExecutionStatus.Running => "执行中",
            ExecutionStatus.Success => "成功",
            ExecutionStatus.PartialSuccess => "部分成功",
            ExecutionStatus.Failed => "失败",
            ExecutionStatus.Timeout => "超时",
            ExecutionStatus.Cancelled => "已取消",
            _ => Status.ToString()
        };

        /// <summary>
        /// 面向界面的业务判定结果：OK、NG 或未判定。由统一执行器根据本次参数中启用的判断项填充。
        /// </summary>
        public string JudgmentStatus { get; set; } = "未判定";

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
            IsPass = false;
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
                IsPass = false,
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
        /// 创建取消结果。取消是独立终态，不应伪装成执行失败。
        /// </summary>
        public static T CreateCancelled<T>(string message = "执行被取消") where T : ToolResults, new()
        {
            return new T
            {
                Status = ExecutionStatus.Cancelled,
                IsPass = false,
                ErrorMessage = message,
                Timestamp = DateTime.Now
            };
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
                new("ExecutionTimeMs", "执行耗时", "执行信息"),
                new("ExecutionStatusText", "执行状态", "执行信息"),
                new("JudgmentStatus", "判定结果", "执行信息"),
                // 仍作为下游绑定的布尔量保留，但不与面向用户的判定结果重复展示。
                new("IsPass", "是否通过", "执行信息", DisplayInResultPanel: false)
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
                    DeclaredType = prop.PropertyType, IsCollection = prop.PropertyType.IsArray, FixedCount = prop.PropertyType.IsArray ? null : 1,
                    VariableType = variableType,
                    DisplayName = entry.DisplayName,
                    Group = entry.GroupName,
                    // P0.6-5：分组占位（单层折叠）随契约传递，Shell 不再按分组名硬编码判断
                    IsGroupPlaceholder = entry.IsGroupPlaceholder,
                    DisplayInResultPanel = entry.DisplayInResultPanel,
                    Kind = OutputVariableKind.Data,
                    RuntimeValue = OutputVariableArrayPromoter.Promote(runtimeValue, variableType, prop.PropertyType)
                });
            }

            // 框架级统一输出：**结构由能力声明决定**（P0.6-1/2/3 根治）。
            //   ① 判定源 = [ToolCapability] 声明（不再用"继承 RoiParameters"推断 —— 那对 14/14 工具恒真）；
            //   ② 四要素（属性名/显示名/组名/类型）= FrameworkContractSchema 单源（不再与 ToolRegistry 各写一遍）；
            //   ③ 结构与值分离：条目只由能力决定，空值渲染占位 "--"，**不删条目**（消除"有时有行有时没行"抖动）。
            var capabilities = ToolCapability.Resolve(this);
            foreach (var entry in FrameworkContractSchema.Resolve(capabilities))
                AddFrameworkContractVariable(variables, entry);

            return variables;
        }

        /// <summary>
        /// 把一条框架级契约条目（检测区/屏蔽区等）追加为输出变量。
        /// </summary>
        /// <remarks>
        /// ★ 值允许为空（null / 空数组）——此时条目<b>仍然产出</b>，由渲染层出占位 <c>--</c>。
        ///   这是「结构与值分离」的要害：结构稳定，才不会出现"有时有行、有时没行"的抖动。
        /// </remarks>
        private void AddFrameworkContractVariable(List<OutputVariable> variables, FrameworkContractEntry entry)
        {
            var prop = GetType().GetProperty(entry.PropertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException(
                    $"工具 '{ToolName ?? GetType().Name}' 声明了能力 '{entry.RequiredCapability}'（需框架契约条目 '{entry.PropertyName}'），但结果类型中不存在该属性（契约漏洞）。");

            var runtimeValue = prop.GetValue(this);
            var variableType = SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ToTypeVocabulary(prop.PropertyType)
                ?? throw new InvalidOperationException(
                    $"工具 '{ToolName ?? GetType().Name}' 的 {entry.PropertyName} 类型 '{prop.PropertyType.Name}' 不是全局变量支持的通用类型，请检查 SdkTypeVocabulary 词汇表。");

            variables.Add(new OutputVariable
            {
                Name = entry.PropertyName,
                DeclaredType = prop.PropertyType, IsCollection = prop.PropertyType.IsArray, FixedCount = prop.PropertyType.IsArray ? null : 1,
                VariableType = variableType,
                DisplayName = entry.DisplayName,
                Group = entry.GroupName,
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
