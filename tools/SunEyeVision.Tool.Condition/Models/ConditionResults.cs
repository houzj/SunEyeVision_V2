using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Condition.Models
{
    /// <summary>
    /// 条件分支执行结果 — 核心输出是 NextNodeId（引擎以此决定分支路径）
    /// </summary>
    public class ConditionResults : ToolResults
    {
        /// <summary>★ 选中的下游节点ID — 引擎执行时的控制信号</summary>
        public string? NextNodeId { get; set; }

        /// <summary>匹配到的规则名称（用于调试和UI显示）</summary>
        public string? MatchedRuleName { get; set; }

        /// <summary>条件评估结果（true = 有规则匹配，false = 无匹配）</summary>
        public bool IsMatched { get; set; }

        public override IReadOnlyList<ResultItem> GetResultItems()
        {
            return new List<ResultItem>
            {
                ResultItem.Boolean("条件匹配", IsMatched),
                ResultItem.Text("匹配规则", MatchedRuleName ?? "无"),
                ResultItem.Text("目标节点ID", NextNodeId ?? "无")
            };
        }

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return new List<PropertyMapEntry>
            {
                new("IsMatched", "条件匹配", "结果"),
                new("MatchedRuleName", "匹配规则", "结果"),
                new("NextNodeId", "目标节点ID", "结果")
            };
        }
    }
}
