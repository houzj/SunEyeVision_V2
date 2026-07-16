using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Condition.Models
{
    /// <summary>
    /// 条件分支执行结果 — 核心输出是 NextNodeId（流程控制信号，引擎强类型读取）。
    /// 实现 IControlFlowResult 使引擎强类型读取分支信号，取代反射 GetProperty("NextNodeId")。
    /// NextNodeId / IsMatched 为控制信号与评估结果，不进数据变量面板 / 绑定候选（控制信号隔离）。
    /// </summary>
    public class ConditionResults : ToolResults, IControlFlowResult
    {
        /// <summary>★ 选中的下游节点ID — 引擎执行时的控制信号（不进数据变量面板 / 绑定候选）</summary>
        public string? NextNodeId { get; set; }

        /// <summary>匹配到的规则名称（用于调试和UI显示）</summary>
        public string? MatchedRuleName { get; set; }

        /// <summary>条件评估结果（true = 有规则匹配，false = 无匹配）</summary>
        public bool IsMatched { get; set; }

        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            return new List<ResultItem>
            {
                ResultItem.Text("匹配规则", MatchedRuleName ?? "无")
            };
        }

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return new List<PropertyMapEntry>
            {
                new("MatchedRuleName", "匹配规则", "结果")
            };
        }
    }
}
