using System.Collections.Generic;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Tool.Condition.Models
{
    /// <summary>
    /// 条件分支参数 — 将条件分支作为标准 IToolPlugin 工具
    /// 输入通过参数绑定获取上游节点结果，输出是选中的下游节点ID (NextNodeId)
    /// </summary>
    public class ConditionParameters : ToolParameters
    {
        /// <summary>条件规则列表（每个规则定义一组判断条件+目标节点）</summary>
        public List<ConditionRule> Rules { get; set; } = new();

        /// <summary>所有规则都不匹配时的默认执行模块ID（为空表示不执行任何分支）</summary>
        public string? DefaultExecutionModuleId { get; set; }
    }

    /// <summary>
    /// 单条条件规则
    /// </summary>
    public class ConditionRule
    {
        /// <summary>规则名称（如 int0, int1）</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>输入数据源绑定（通过 AvailableDataSource 动态绑定上游节点属性）</summary>
        public AvailableDataSource? InputBinding { get; set; }

        /// <summary>有效范围下限</summary>
        public double MinValue { get; set; }

        /// <summary>有效范围上限</summary>
        public double MaxValue { get; set; } = 1000.0;

        /// <summary>判断类型: OK（范围内匹配）/ NG（范围外匹配）</summary>
        public string Judgment { get; set; } = "OK";

        /// <summary>条件满足时执行的下游节点ID（★ 引擎根据此ID决定分支路径）</summary>
        public string ExecutionModuleId { get; set; } = string.Empty;
    }
}
