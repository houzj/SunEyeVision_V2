namespace SunEyeVision.Plugin.SDK.Execution.Results
{
    /// <summary>
    /// 输出变量契约（SSOT）。变量即契约，来源于工具定义/参数（设计时已知），
    /// 而非运行时结果实例反射。变量名 + 类型复用 SdkTypeVocabulary 固定词汇。
    /// 图形显示、结果面板、下游绑定三处 UI 全部从此契约读取，静态/动态工具走同一条路。
    /// </summary>
    public sealed class OutputVariable
    {
        public System.Guid? FieldId { get; set; }
        /// <summary>Declared, never inferred from a previous result's value or length.</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public System.Type? DeclaredType { get; set; }
        public bool? IsCollection { get; set; }
        public int? FixedCount { get; set; }
        /// <summary>变量名（工具内唯一，对应绑定路径的 PropertyName）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>变量类型（全局变量类型字符串，如 "Image"/"Double"，复用 SdkTypeVocabulary 词汇）</summary>
        public string VariableType { get; set; } = string.Empty;

        /// <summary>中文显示名（用于 UI 展示，空则回退 Name）</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>分组（用于树形分组，null 表示无分组）</summary>
        public string? Group { get; set; }

        /// <summary>
        /// 分组占位条目（单层折叠）：该条目不渲染为中间节点，其展开子节点直接挂到 <see cref="Group"/> 下。
        /// 由 <c>PropertyMapEntry.IsGroupPlaceholder</c> 随契约传递（方案 P0.6-5 声明式折叠）。
        /// </summary>
        public bool IsGroupPlaceholder { get; set; }

        /// <summary>是否在模块结果面板展示。false 时仍是可绑定的输出变量。</summary>
        public bool DisplayInResultPanel { get; set; } = true;

        /// <summary>变量种类：数据变量 / 流程控制信号</summary>
        public OutputVariableKind Kind { get; set; } = OutputVariableKind.Data;

        /// <summary>运行期填充的实际值（设计时契约无值则为 null）</summary>
        public object? RuntimeValue { get; set; }
    }

    /// <summary>
    /// 输出变量种类
    /// </summary>
    public enum OutputVariableKind
    {
        /// <summary>数据变量（进结果面板 / 绑定候选 / 图像源）</summary>
        Data,

        /// <summary>流程控制信号（如条件分支 NextNodeId，仅供引擎强类型消费，不进数据面板 / 绑定候选）</summary>
        ControlSignal
    }
}
