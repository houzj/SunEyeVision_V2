using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Condition.Models
{
    /// <summary>
    /// 条件分支参数 — 将条件分支作为标准 IToolPlugin 工具
    /// 输入通过参数绑定获取上游节点结果，输出是选中的下游节点ID (NextNodeId)
    /// </summary>
    /// <remarks>
    /// rule-010：多态序列化必须带 [JsonDerivedType]，鉴别符与 ToolRegistry 注册一致，
    /// 否则 ToolParameters 反序列化无法恢复为 ConditionParameters（会静默落到 GenericToolParameters）。
    /// </remarks>
    [JsonDerivedType(typeof(ConditionParameters), "ConditionParameters")]
    public class ConditionParameters : ToolParameters
    {
        /// <summary>条件规则列表（每个规则定义一组判断条件+目标节点）</summary>
        public ObservableCollection<ConditionRule> Rules { get; set; } = new();
    }

    /// <summary>
    /// 单条条件规则
    /// </summary>
    /// <remarks>
    /// 契约 C1：InputText 是唯一真相源（单轨），两种形态并存：
    /// - 绑定表达式：节点GUID.属性名（如 "e2a8c5d7-f3e2-4b1a-9c5d-8f7e6b5a4c3d.面积"）
    /// - 常量：字面量（如 "123" / "3.5" / "OK"）
    /// 禁止引入 BindingSource / IsBound 第二存储位——所见即所存、所存即所解析。
    /// </remarks>
    public class ConditionRule : ObservableObject
    {
        private string _name = string.Empty;
        private string _ruleType = "Int";
        private string _inputText = string.Empty;
        private double _minValue;
        private double _maxValue = 1000.0;
        private string _judgment = "OK";
        private string _executionModuleId = string.Empty;

        /// <summary>规则名称（如 int0, int1）</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, "规则名称");
        }

        /// <summary>规则类型：Int / Double / String</summary>
        public string RuleType
        {
            get => _ruleType;
            set => SetProperty(ref _ruleType, value, "规则类型");
        }

        /// <summary>★ 条件：绑定表达式（节点GUID.属性名）或 常量（字面量）。唯一真相源。</summary>
        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value, "条件");
        }

        /// <summary>有效范围下限</summary>
        public double MinValue
        {
            get => _minValue;
            set => SetProperty(ref _minValue, value, "有效范围下限");
        }

        /// <summary>有效范围上限</summary>
        public double MaxValue
        {
            get => _maxValue;
            set => SetProperty(ref _maxValue, value, "有效范围上限");
        }

        /// <summary>判断类型: OK（范围内匹配）/ NG（范围外匹配）</summary>
        public string Judgment
        {
            get => _judgment;
            set => SetProperty(ref _judgment, value, "判断");
        }

        /// <summary>条件满足时执行的下游节点ID（★ 引擎根据此ID决定分支路径）</summary>
        public string ExecutionModuleId
        {
            get => _executionModuleId;
            set => SetProperty(ref _executionModuleId, value, "目标节点");
        }
    }
}
