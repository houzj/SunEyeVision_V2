using System;
using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Tool.Condition.Models;

namespace SunEyeVision.Tool.Condition
{
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
        public System.Windows.FrameworkElement? CreateDebugControl(IToolPlugin tool)
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

                foreach (var rule in parameters.Rules)
                {
                    if (string.IsNullOrEmpty(rule.ExecutionModuleId))
                    {
                        parameters.LogWarning($"规则 '{rule.Name}' 未配置执行模块ID，跳过");
                        continue;
                    }

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

                    // 按类型匹配
                    bool matched = TryMatchRule(rule, rawValue, parameters);

                    parameters.LogInfo($"规则 '{rule.Name}': 输入值类型={rawValue.GetType().Name}, 值={rawValue}, 匹配={matched}");

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

        /// <summary>
        /// 尝试匹配规则，支持数值比较、字符串精确匹配、bool 匹配。
        /// 不匹配时返回 false（但不会静默吞异常——类型转换失败会向上抛）。
        /// </summary>
        private static bool TryMatchRule(ConditionRule rule, object rawValue, ConditionParameters parameters)
        {
            // 1. 数值匹配（IConvertible → double）
            if (rawValue is IConvertible convertible)
            {
                double numValue = convertible.ToDouble(null);
                bool inRange = numValue >= rule.MinValue && numValue <= rule.MaxValue;
                bool matched = (rule.Judgment == "OK" && inRange) ||
                               (rule.Judgment == "NG" && !inRange);
                parameters.LogInfo($"规则 '{rule.Name}': 数值匹配, 输入={numValue}, 范围=[{rule.MinValue},{rule.MaxValue}], 判断={rule.Judgment}, 匹配={matched}");
                return matched;
            }

            // 2. 字符串精确匹配
            if (rawValue is string strValue)
            {
                string minStr = rule.MinValue.ToString();
                string maxStr = rule.MaxValue.ToString();
                bool inRange = string.Compare(strValue, minStr, StringComparison.OrdinalIgnoreCase) >= 0 &&
                               string.Compare(strValue, maxStr, StringComparison.OrdinalIgnoreCase) <= 0;
                bool matched = (rule.Judgment == "OK" && inRange) ||
                               (rule.Judgment == "NG" && !inRange);
                parameters.LogInfo($"规则 '{rule.Name}': 字符串匹配, 值='{strValue}', 范围=[{minStr},{maxStr}], 匹配={matched}");
                return matched;
            }

            // 3. 类型不匹配—不再静默返回 false，而是让异常向上传播
            throw new InvalidOperationException(
                $"规则 '{rule.Name}' 输入值类型 {rawValue.GetType().Name} 不支持条件比较。支持的类型: 数值、字符串");
        }
    }
}
