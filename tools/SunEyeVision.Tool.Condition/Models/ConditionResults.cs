using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Condition.Models
{
    /// <summary>
    /// 条件分支执行结果 — 核心输出是 NextNodeIds（流程控制信号，引擎强类型读取）。
    /// 实现 IControlFlowResult 使引擎强类型读取分支信号，取代反射 GetProperty("NextNodeId")。
    /// NextNodeIds / IsMatched 为控制信号与评估结果，不进数据变量面板 / 绑定候选（控制信号隔离）。
    /// 多值：支持多条规则同时命中（多分支并行）；未命中时为空集合（引擎剪枝终止该分支）。
    /// </summary>
    public class ConditionResults : ToolResults, IControlFlowResult
    {
        /// <summary>★ 所有命中的下游节点ID — 引擎执行时的控制信号（多选并行；不进数据变量面板 / 绑定候选）</summary>
        public List<string> NextNodeIds { get; set; } = new();

        // 显式实现接口：公开属性用 List（便于 Add 收集），接口返回 IReadOnlyList（协变只读）
        IReadOnlyList<string> IControlFlowResult.NextNodeIds => NextNodeIds;

        /// <summary>流程控制信号总为 true（条件分支总是产生控制，命中走分支、未命中剪枝终止）</summary>
        public bool HasControlSignal => true;

        /// <summary>匹配到的规则名称列表（用于调试和UI显示）</summary>
        public List<string> MatchedRuleNames { get; set; } = new();

        /// <summary>条件评估结果（true = 有规则匹配，false = 无匹配）</summary>
        public bool IsMatched { get; set; }

        public override List<PropertyMapEntry> GetPropertyMap()
        {
            return
            [
                .. base.GetPropertyMap(),   // 合并基类：ExecutionTimeMs → 执行耗时/执行信息
                new("MatchedRuleNames", "匹配规则", "结果")
            ];
        }
    }
}
