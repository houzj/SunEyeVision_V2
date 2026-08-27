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
        Description = "根据参数值判断条件，选择执行分支",
        Icon = "\U0001F500",
        Category = "工作流控制",
        Version = "1.0.0",
        HasDebugWindow = true
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

            if (parameters.Rules == null || parameters.Rules.Count == 0)
            {
                parameters.LogInfo("条件分支未配置任何规则，流程终止（不执行任何分支）");
                result.IsMatched = false;
                sw.Stop();
                result.SetSuccess(sw.ElapsedMilliseconds);
                return result;
            }

            parameters.LogInfo($"开始评估条件分支，共 {parameters.Rules.Count} 条规则");

            // ★ 多选并行：遍历所有规则，收集所有命中的目标节点（不遇命中即 return）。
            //   未命中的规则不产生目标；全部未命中则 NextNodeIds 为空 → 引擎剪枝终止该分支。
            foreach (var rule in parameters.Rules)
            {
                if (string.IsNullOrEmpty(rule.ExecutionModuleId))
                {
                    parameters.LogWarning($"规则 '{rule.Name}' 未配置目标节点（ExecutionModuleId），跳过");
                    continue;
                }

                // 解析输入值：绑定模式委托 OutputVariableResolver，常量模式按 RuleType 转换
                var inputValue = ConditionInputResolver.Resolve(
                    rule.InputText, rule.RuleType, rule.Name, parameters.NodeResults);

                var matched = TryMatchRule(rule, inputValue, parameters);

                parameters.LogInfo($"规则 '{rule.Name}': 类型={rule.RuleType}, 输入={inputValue}, 匹配={matched}");

                if (matched)
                {
                    result.NextNodeIds.Add(rule.ExecutionModuleId);
                    result.MatchedRuleNames.Add(rule.Name);
                    result.IsMatched = true;
                    parameters.LogSuccess($"条件匹配: 规则='{rule.Name}', 目标节点ID='{rule.ExecutionModuleId}'");
                }
            }

            // 全部未命中 → NextNodeIds 为空 → 引擎剪枝终止该分支（对齐 VisionMaster，无默认分支）
            if (!result.IsMatched)
            {
                parameters.LogInfo("无规则匹配，该分支流程终止（后续节点不执行）");
            }

            sw.Stop();
            result.SetSuccess(sw.ElapsedMilliseconds);
            return result;
        }

        /// <summary>
        /// 按规则类型匹配输入值。
        /// Int/Double：数值在 [MinValue, MaxValue] 内且 Judgment=OK（或范围外且 NG）→ 命中。
        /// String：配置期望值 ExpectedText 时，OK=相等 / NG=不相等（区分大小写）才命中；
        ///         未配置期望值时，输入值有效（非 null）即命中（旧行为向后兼容）。
        /// 类型不匹配 / 无法转换 → 抛异常（rule-015，不静默）。
        /// </summary>
        private static bool TryMatchRule(ConditionRule rule, object? inputValue, ConditionParameters parameters)
        {
            switch (rule.RuleType)
            {
                case "Int":
                case "Double":
                    if (inputValue == null)
                    {
                        parameters.LogInfo($"规则 '{rule.Name}': 输入值为空，不匹配");
                        return false;
                    }
                    // 数值转换：数字类型直接转换，字符串用 InvariantCulture 解析
                    double numValue;
                    if (inputValue is IConvertible convertible)
                    {
                        numValue = convertible.ToDouble(null);
                    }
                    else if (inputValue is string strVal)
                    {
                        if (!double.TryParse(strVal, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out numValue))
                        {
                            throw new InvalidOperationException(
                                $"规则 '{rule.Name}' 输入值 \"{strVal}\" 无法转换为数值（RuleType={rule.RuleType}）。");
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            $"规则 '{rule.Name}' 输入值类型 {inputValue.GetType().Name} 不支持数值比较。支持: 数值、数字字符串。");
                    }

                    bool inRange = numValue >= rule.MinValue && numValue <= rule.MaxValue;
                    bool matched = (rule.Judgment == "OK" && inRange) ||
                                   (rule.Judgment == "NG" && !inRange);
                    parameters.LogInfo($"规则 '{rule.Name}': 数值匹配, 输入={numValue}, 范围=[{rule.MinValue},{rule.MaxValue}], 判断={rule.Judgment}, 匹配={matched}");
                    return matched;

                case "String":
                    if (inputValue == null)
                    {
                        parameters.LogInfo($"规则 '{rule.Name}': 字符串输入值为空，不匹配");
                        return false;
                    }
                    // 未配置期望值：保持旧"非空即命中"行为（向后兼容旧数据）
                    if (string.IsNullOrEmpty(rule.ExpectedText))
                    {
                        parameters.LogInfo($"规则 '{rule.Name}': 字符串规则, 输入值有效='{inputValue}', 命中");
                        return true;
                    }
                    // 配置了期望值：OK=相等（区分大小写），NG=不相等
                    bool eqStr = string.Equals(
                        inputValue.ToString(), rule.ExpectedText, StringComparison.Ordinal);
                    bool strMatched = (rule.Judgment == "OK" && eqStr) || (rule.Judgment == "NG" && !eqStr);
                    parameters.LogInfo($"规则 '{rule.Name}': 字符串比较, 输入='{inputValue}', 期望='{rule.ExpectedText}', 判断={rule.Judgment}, 匹配={strMatched}");
                    return strMatched;

                default:
                    throw new NotSupportedException(
                        $"规则 '{rule.Name}' 的类型 '{rule.RuleType}' 不支持。可用类型: Int / Double / String。");
            }
        }
    }
}
