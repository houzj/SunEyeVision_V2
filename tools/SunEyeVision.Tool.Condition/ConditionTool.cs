using System;
using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Condition.Models;

namespace SunEyeVision.Tool.Condition
{
    /// <summary>
    /// 条件分支工具 — 作为标准 IToolPlugin，经由 ToolRegistry 注册和管理。
    /// Run() 评估规则表，输出选中下游节点的 ID (NextNodeId)。
    /// 工作流引擎读取 NextNodeId 决定执行哪条分支路径。
    /// </summary>
    [Tool(
        id: "Condition",
        displayName: "条件分支",
        Description = "根据参数绑定值判断条件，选择执行分支",
        Icon = "\U0001F500",
        Category = "工作流控制",
        Version = "1.0.0",
        HasDebugWindow = false
    )]
    public class ConditionTool : IToolPlugin<ConditionParameters, ConditionResults>
    {
        public System.Windows.FrameworkElement? CreateDebugControl()
        {
            return new Views.ConditionDebugControl();
        }

        public ConditionResults Run(ConditionParameters parameters)
        {
            var sw = Stopwatch.StartNew();
            var result = new ConditionResults
            {
                ToolName = "条件分支",
                ToolId = "Condition",
                Timestamp = DateTime.Now
            };

            try
            {
                if (parameters.Rules == null || parameters.Rules.Count == 0)
                {
                    parameters.LogInfo("条件分支未配置任何规则，所有分支将被跳过");
                    result.NextNodeId = parameters.DefaultExecutionModuleId;
                    result.MatchedRuleName = "默认";
                    result.IsMatched = false;
                    sw.Stop();
                    result.SetSuccess(sw.ElapsedMilliseconds);
                    return result;
                }

                parameters.LogInfo($"开始评估条件分支，共 {parameters.Rules.Count} 条规则");

                // 按顺序评估每条规则（第一条匹配即停止）
                foreach (var rule in parameters.Rules)
                {
                    if (string.IsNullOrEmpty(rule.ExecutionModuleId))
                    {
                        parameters.LogWarning($"规则 '{rule.Name}' 未配置执行模块ID，跳过");
                        continue;
                    }

                    // 从参数绑定获取实际值
                    object? rawValue = null;
                    if (rule.InputBinding?.CurrentValue != null)
                    {
                        rawValue = rule.InputBinding.CurrentValue;
                    }

                    if (rawValue == null)
                    {
                        parameters.LogInfo($"规则 '{rule.Name}' 输入值为空，跳过");
                        continue;
                    }

                    // 转换为数值
                    if (!TryConvertToDouble(rawValue, out double numValue))
                    {
                        parameters.LogWarning($"规则 '{rule.Name}' 输入值无法转换为数值: {rawValue}");
                        continue;
                    }

                    bool inRange = numValue >= rule.MinValue && numValue <= rule.MaxValue;
                    bool matched = (rule.Judgment == "OK" && inRange) ||
                                   (rule.Judgment == "NG" && !inRange);

                    parameters.LogInfo($"规则 '{rule.Name}': 输入={numValue}, 范围=[{rule.MinValue},{rule.MaxValue}], 判断={rule.Judgment}, 范围内={inRange}, 匹配={matched}");

                    if (matched)
                    {
                        result.NextNodeId = rule.ExecutionModuleId;
                        result.MatchedRuleName = rule.Name;
                        result.IsMatched = true;
                        parameters.LogSuccess($"条件匹配: 规则='{rule.Name}', 目标节点ID='{rule.ExecutionModuleId}'");
                        sw.Stop();
                        result.SetSuccess(sw.ElapsedMilliseconds);
                        return result;
                    }
                }

                // 无规则匹配，使用默认值
                result.NextNodeId = parameters.DefaultExecutionModuleId;
                result.MatchedRuleName = result.NextNodeId != null ? "默认" : "无匹配";
                result.IsMatched = false;
                parameters.LogInfo($"无规则匹配，使用默认执行模块ID: {result.NextNodeId ?? "无（跳过所有分支）"}");

                sw.Stop();
                result.SetSuccess(sw.ElapsedMilliseconds);
                return result;
            }
            catch (Exception ex)
            {
                sw.Stop();
                parameters.LogError($"条件分支执行异常: {ex.Message}", ex);
                result.SetError($"条件分支异常: {ex.Message}", ex);
                return result;
            }
        }

        private static bool TryConvertToDouble(object value, out double result)
        {
            result = 0;
            try
            {
                if (value is IConvertible convertible)
                {
                    result = convertible.ToDouble(null);
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
