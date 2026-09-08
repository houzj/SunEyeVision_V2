using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 节点结果参数上下文 - IParameterContext 的 SDK 公共实现。
    /// 将节点执行结果字典（IDictionary&lt;string, ToolResults&gt;）适配为订阅解析上下文，
    /// 供区域订阅（FixedRoi/ComputedRoi）与适配层（HalAdapter/CvAdapter）统一取值。
    /// </summary>
    /// <remarks>
    /// 定位与设计原则：
    /// - 平台级订阅链路的公共取值通道（SSOT）：GetNodeOutputValue 统一委托
    ///   OutputVariableResolver 按输出变量契约寻址，与 Condition/Script 绑定同一口径，
    ///   取代各处独立反射 + 手动数组/嵌套寻址的重复实现。
    /// - 历史实现（HalAdapter 私有 NodeResultsParameterContext）用反射直取 CLR 属性，
    ///   不校验输出变量契约；本公共实现改为契约寻址，未注册输出/节点缺失直接报错
    ///   （rule-015 零静默回退），杜绝"绑定配置错误却静默解析失败"。
    /// - 路径语义：outputName 为输出变量契约名；index 为数组下标；propertyPath 为
    ///   OutputVariableResolver 完整路径（可含 [i] / [Type=X] / 嵌套字段，如
    ///   "Circle[0].Center.X"）。propertyPath 非空时以它为准，否则由 outputName+index 组合。
    /// </remarks>
    public class NodeResultsParameterContext : IParameterContext
    {
        private readonly IDictionary<string, ToolResults> _nodeResults;

        /// <summary>
        /// 创建节点结果参数上下文
        /// </summary>
        /// <param name="nodeResults">节点执行结果字典（nodeId → ToolResults）</param>
        public NodeResultsParameterContext(IDictionary<string, ToolResults> nodeResults)
        {
            _nodeResults = nodeResults ?? throw new ArgumentNullException(nameof(nodeResults));
        }

        /// <inheritdoc/>
        public object? GetNodeOutputValue(string nodeId, string outputName, int? index = null, string? propertyPath = null)
        {
            if (string.IsNullOrEmpty(nodeId))
                throw new ArgumentException("nodeId 不能为空", nameof(nodeId));

            if (!_nodeResults.TryGetValue(nodeId, out var result) || result == null)
                throw new InvalidOperationException(
                    $"订阅源节点 '{nodeId}' 不在执行结果中。可用节点: [{string.Join(", ", _nodeResults.Keys)}]。请检查该上游节点本次流程是否已执行。");

            // propertyPath 非空 → 已含完整契约路径（可含 [i]/[Type=X]/嵌套字段），直接按契约寻址
            if (!string.IsNullOrEmpty(propertyPath))
                return OutputVariableResolver.Resolve(_nodeResults, nodeId, propertyPath);

            if (string.IsNullOrEmpty(outputName))
                throw new InvalidOperationException(
                    $"节点 '{nodeId}' 的订阅未配置输出变量名（outputName 为空）。");

            // 组合 outputName + 显式数组下标 → 契约寻址（SSOT）
            var path = index.HasValue ? $"{outputName}[{index.Value}]" : outputName;
            return OutputVariableResolver.Resolve(_nodeResults, nodeId, path);
        }
    }
}
