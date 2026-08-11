using System;
using System.Collections.Generic;
using System.Globalization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Tool.Condition.Models
{
    /// <summary>
    /// 条件分支输入解析器 — 把 ConditionRule.InputText 解析为统一的"输入值"。
    /// 薄层：只做"模式判定 + 常量转换"，绑定那半委托 OutputVariableResolver（SDK SSOT 复用）。
    /// </summary>
    /// <remarks>
    /// 契约 C3：模式判定 = GUID 格式 + 结果集取值，两层职责分离：
    /// - GUID 格式管"是不是绑定表达式"（节点 ID 设计钉死为 GUID，见 src/Workflow/WorkflowNode.cs:45-52）
    /// - NodeResults 管"取不取得到值"（取不到就明确报"节点不在执行结果中"，而非误判常量）
    ///
    /// 判定流程（看文本结构，不看状态/是否绑定过）：
    /// ① 文本含 '.' → 按第一个 '.' 切分：nodeId = 左段，propertyPath = 右段
    /// ② nodeId 非空 且 Guid.TryParse 成功 → 绑定模式，委托 OutputVariableResolver
    /// ③ 否则 → 常量模式，按 RuleType 转换（Int/Double 用 InvariantCulture，String 原文即值）
    /// 解析失败 → 抛异常（rule-015，禁止静默）
    /// </remarks>
    public static class ConditionInputResolver
    {
        /// <summary>
        /// 解析 InputText 为统一的输入值（绑定模式取上游值 / 常量模式按 RuleType 转换）。
        /// </summary>
        /// <param name="inputText">条件输入文本（唯一真相源）</param>
        /// <param name="ruleType">规则类型（Int / Double / String）</param>
        /// <param name="ruleName">规则名（仅用于错误信息）</param>
        /// <param name="nodeResults">已执行节点结果字典（由 ToolExecutor 注入，null 视为空）</param>
        /// <returns>解析后的输入值</returns>
        public static object? Resolve(
            string inputText,
            string ruleType,
            string ruleName,
            IDictionary<string, ToolResults>? nodeResults)
        {
            if (TrySplitBinding(inputText, out var nodeId, out var propertyPath))
            {
                return ResolveBinding(nodeId, propertyPath, ruleName, nodeResults);
            }

            return ResolveConstant(inputText, ruleType, ruleName);
        }

        /// <summary>
        /// 尝试按绑定表达式切分：① 含 '.' ② 节点ID段是合法 GUID。
        /// 任一不满足 → 非绑定表达式（常量）。
        /// </summary>
        private static bool TrySplitBinding(string inputText, out string nodeId, out string propertyPath)
        {
            nodeId = string.Empty;
            propertyPath = string.Empty;

            if (string.IsNullOrEmpty(inputText))
                return false;

            var dotIndex = inputText.IndexOf('.');
            if (dotIndex < 0)
                return false;

            var candidate = inputText.Substring(0, dotIndex);
            var path = inputText.Substring(dotIndex + 1);

            // 契约 C3：节点ID必须是 GUID。常量 "3.5" 的节点段 "3" 不是 GUID → 落常量模式（防误判）
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(path))
                return false;

            if (!Guid.TryParse(candidate, out _))
                return false;

            nodeId = candidate;
            propertyPath = path;
            return true;
        }

        /// <summary>
        /// 绑定模式：委托 OutputVariableResolver（SSOT）。
        /// 节点未执行/不在结果集 → 抛出"节点不在执行结果中"（准确提示，而非误判常量）。
        /// </summary>
        private static object? ResolveBinding(
            string nodeId,
            string propertyPath,
            string ruleName,
            IDictionary<string, ToolResults>? nodeResults)
        {
            var results = nodeResults ?? new Dictionary<string, ToolResults>();
            if (!results.ContainsKey(nodeId))
            {
                throw new InvalidOperationException(
                    $"规则 '{ruleName}' 绑定节点 '{nodeId}' 不在执行结果中。可用节点: [{string.Join(", ", results.Keys)}]。请检查该上游节点本次流程是否已执行。");
            }

            return OutputVariableResolver.Resolve(results, nodeId, propertyPath);
        }

        /// <summary>
        /// 常量模式：按 RuleType 转换。
        /// Int/Double → InvariantCulture 解析，失败抛异常；String → 原文即值（不剥引号，KISS）。
        /// </summary>
        private static object? ResolveConstant(string inputText, string ruleType, string ruleName)
        {
            switch (ruleType)
            {
                case "Int":
                    if (int.TryParse(inputText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                        return intValue;
                    throw new InvalidOperationException(
                        $"规则 '{ruleName}' 的常量条件 \"{inputText}\" 无法解析为 Int 类型。请输入整数（如 1、-5）。");

                case "Double":
                    if (double.TryParse(inputText, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                        return doubleValue;
                    throw new InvalidOperationException(
                        $"规则 '{ruleName}' 的常量条件 \"{inputText}\" 无法解析为 Double 类型。请输入数字（如 3.5、-1.2）。");

                case "String":
                    if (string.IsNullOrEmpty(inputText))
                        throw new InvalidOperationException($"规则 '{ruleName}' 未配置条件（InputText 为空）。");
                    return inputText;

                default:
                    throw new NotSupportedException(
                        $"规则 '{ruleName}' 的类型 '{ruleType}' 不支持。可用类型: Int / Double / String。");
            }
        }
    }
}
