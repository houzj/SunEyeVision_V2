using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.VisionAdapters;
using SunEyeVision.Tool.Subroutine.Models;
using SunEyeVision.Workflow;
using ExecutionResult = SunEyeVision.Workflow.ExecutionResult;

namespace SunEyeVision.Tool.Subroutine
{
    /// <summary>
    /// 子程序工具 — 本质上是一个"大号工具"，
    /// 内部通过工作流引擎执行子工作流，将结果映射到标准 ToolResults 输出。
    /// </summary>
    [Tool(
        id: "Subroutine",
        displayName: "子程序",
        Description = "执行子工作流（大号工具，内部使用工作流节点组织）",
        Icon = "\U0001F501",
        Category = "工作流控制",
        Version = "1.0.0",
        HasDebugWindow = false
    )]
    public class SubroutineTool : IToolPlugin<SubroutineParameters, SubroutineResults>
    {
        /// <summary>
        /// 执行子程序逻辑
        /// </summary>
        public SubroutineResults Run(SubroutineParameters parameters)
        {
            var sw = Stopwatch.StartNew();
            var result = new SubroutineResults
            {
                ToolName = "子程序",
                ToolId = "Subroutine",
                Timestamp = DateTime.Now
            };

            try
            {
                // ===== 前置验证 =====
                if (string.IsNullOrEmpty(parameters.SubroutineId))
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 未配置目标工作流");

                if (parameters.ExecuteSubWorkflowAsync == null)
                    throw new InvalidOperationException("子工作流执行委托未注入，请确保通过 WorkflowExecutionEngine 执行包含子程序节点的工作流");

                var inputImage = parameters.ResolvedInputImage;

                parameters.LogInfo($"开始执行子程序: {parameters.SubroutineName}, 工作流ID: {parameters.SubroutineId}, 输入图像: {(inputImage != null ? $"{inputImage.Width}x{inputImage.Height}" : "null")}");

                // ===== 解析输入参数绑定（与标准工具一致：在 Run() 内主动解析）=====
                var contextVariables = ResolveInputs(parameters);
                if (contextVariables.Count > 0)
                {
                    parameters.LogInfo($"  已解析 {contextVariables.Count} 个输入参数绑定");
                }

                // ===== 执行子工作流 =====
                ExecutionResult execResult;
                if (parameters.IsLoopEnabled)
                {
                    execResult = ExecuteWithLoop(parameters, inputImage, contextVariables);
                }
                else
                {
                    execResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                }

                // ===== 收集结果 =====
                var subWorkflowNodeResults = new Dictionary<string, ToolResults>();
                if (execResult.NodeResults != null)
                {
                    foreach (var kvp in execResult.NodeResults)
                    {
                        if (kvp.Value?.Result != null)
                        {
                            result.NodeOutputs[kvp.Key] = kvp.Value.Result;
                            subWorkflowNodeResults[kvp.Key] = kvp.Value.Result;
                        }
                    }
                }

                // 捕获输出配置快照（供结果模块树在设计态展示动态输出端口）
                result.OutputConfigs = parameters.Outputs.ToList();

                // ===== 解析输出项（根据 Outputs 配置，从子工作流节点结果中取值）=====
                if (subWorkflowNodeResults.Count > 0)
                {
                    var resolver = new ParameterResolver(parameters.Context?.Logger);
                    foreach (var output in parameters.Outputs.Where(o => o.Source != null))
                    {
                        var source = output.Source!;
                        var binding = new ParamSetting
                        {
                            ParameterName = output.Name,
                            BindingType = BindingType.Binding,
                            SourceNodeId = source.SourceNodeId,
                            SourceProperty = source.PropertyName,
                            TargetType = source.PropertyType ?? typeof(object)
                        };
                        var resolveResult = resolver.Resolve(binding, subWorkflowNodeResults);
                        if (resolveResult.IsSuccess && resolveResult.Value != null)
                        {
                            result.ResolvedOutputValues[output.Name] = resolveResult.Value;
                            parameters.LogInfo($"  输出 '{output.Name}' = {source.SourceNodeId}.{source.PropertyName} = {resolveResult.Value}");
                        }
                        else
                        {
                            parameters.LogWarning($"  输出 '{output.Name}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                        }
                    }
                }

                result.ExecutedNodeCount = execResult.NodeResults?.Count ?? 0;
                result.SubWorkflowExecutionTimeMs = (long)execResult.ExecutionTime.TotalMilliseconds;

                if (!execResult.Success && execResult.Errors != null && execResult.Errors.Any())
                {
                    var errorMsg = string.Join("; ", execResult.Errors);
                    parameters.LogError($"子程序执行失败: {errorMsg}");
                    result.SetError(errorMsg);
                }
                else
                {
                    sw.Stop();
                    result.SetSuccess(sw.ElapsedMilliseconds);
                    parameters.LogSuccess($"子程序执行完成: {result.ExecutedNodeCount} 个节点, 耗时 {result.SubWorkflowExecutionTimeMs}ms");
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                parameters.LogError($"子程序异常: {ex.Message}", ex);
                result.SetError($"子程序执行异常: {ex.Message}", ex);
            }

            return result;
        }

        private static ExecutionResult ExecuteSubWorkflowOnce(SubroutineParameters parameters, IVisionImage? inputImage,
            Dictionary<string, object> contextVariables)
        {
            if (parameters.ExecuteSubWorkflowAsync == null)
                throw new InvalidOperationException("子工作流执行委托未注入");

            return Task.Run(async () =>
                await parameters.ExecuteSubWorkflowAsync(parameters.SubroutineId, inputImage, contextVariables))
                .GetAwaiter().GetResult();
        }

        /// <summary>
        /// 解析输入参数 — 从父工作流的 NodeResults 中根据 Inputs[].Source 取值
        /// </summary>
        private static Dictionary<string, object> ResolveInputs(SubroutineParameters parameters)
        {
            var values = new Dictionary<string, object>();
            var nodeResults = parameters.NodeResults;
            if (nodeResults == null) return values;

            var resolver = new ParameterResolver(parameters.Context?.Logger);

            foreach (var input in parameters.Inputs.Where(i => i.Source != null))
            {
                var src = input.Source!;
                var binding = new ParamSetting
                {
                    ParameterName = input.Name,
                    BindingType = BindingType.Binding,
                    SourceNodeId = src.SourceNodeId,
                    SourceProperty = src.PropertyName,
                    TargetType = src.PropertyType ?? typeof(object)
                };
                var resolveResult = resolver.Resolve(binding, nodeResults);
                if (resolveResult.IsSuccess && resolveResult.Value != null)
                {
                    values[input.Name] = resolveResult.Value;
                    parameters.LogInfo($"  输入 '{input.Name}' <- {src.SourceNodeName}.{src.DisplayName} = {resolveResult.Value}");
                }
                else
                {
                    parameters.LogWarning($"  输入 '{input.Name}' 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");
                }
            }
            return values;
        }

        private static ExecutionResult ExecuteWithLoop(SubroutineParameters parameters, IVisionImage? inputImage,
            Dictionary<string, object> contextVariables)
        {
            var config = parameters.LoopConfig;
            var result = new ExecutionResult();

            // ===== 解析循环起止值（始终从绑定源获取）=====
            var resolver = new ParameterResolver(parameters.Context?.Logger);
            var nodeResults = parameters.NodeResults;

            int start = ResolveLoopBound(config.StartValueBinding, "循环起始值", resolver, nodeResults);
            int end = ResolveLoopBound(config.EndValueBinding, "循环结束值", resolver, nodeResults);

            parameters.LogInfo($"循环起止: [{start}, {end}), 中断循环: {config.EnableBreak}, 共 {end - start} 次");

            for (int i = start; i < end; i++)
            {
                parameters.CurrentIteration = i;
                var iterResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                result.Merge(iterResult);

                if (!iterResult.Success)
                    break;

                // ===== 中断条件判断 =====
                if (config.EnableBreak)
                {
                    bool shouldBreak = EvaluateBreakCondition(config, resolver, nodeResults, iterResult, parameters);
                    if (shouldBreak)
                    {
                        parameters.LogInfo($"循环在第 {i} 次迭代后触发中断条件，提前结束");
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 解析循环边界值：必须通过绑定源获取，不支持字面量
        /// </summary>
        private static int ResolveLoopBound(string bindingPath, string displayName,
            ParameterResolver resolver, IDictionary<string, ToolResults>? nodeResults)
        {
            if (string.IsNullOrEmpty(bindingPath))
                throw new InvalidOperationException($"{displayName} 未配置绑定源，请先点击 🔗 选择上游节点输出");

            if (nodeResults == null)
                throw new InvalidOperationException($"节点结果为空，无法解析 {displayName} 绑定");

            var parts = bindingPath.Split('.');
            if (parts.Length < 2)
                throw new InvalidOperationException($"{displayName} 绑定源格式无效: '{bindingPath}'，应为 \"节点ID.属性名\"");

            var binding = new ParamSetting
            {
                ParameterName = displayName,
                BindingType = BindingType.Binding,
                SourceNodeId = parts[0],
                SourceProperty = parts[1],
                TargetType = typeof(int)
            };
            var resolveResult = resolver.Resolve(binding, nodeResults);
            if (!resolveResult.IsSuccess || resolveResult.Value == null)
                throw new InvalidOperationException($"{displayName} 绑定解析失败: {resolveResult.ErrorMessage ?? "值为空"}");

            return Convert.ToInt32(resolveResult.Value);
        }

        /// <summary>
        /// 评估循环中断条件：源/目的比较值均必须通过绑定获取
        /// </summary>
        private static bool EvaluateBreakCondition(SubroutineLoopConfig config,
            ParameterResolver resolver, IDictionary<string, ToolResults>? nodeResults,
            ExecutionResult iterResult, SubroutineParameters parameters)
        {
            if (!config.EnableBreak) return false;

            // 源比较值：从绑定源解析（当前迭代结果）
            string? sourceStr = null;
            if (!string.IsNullOrEmpty(config.SourceCompareBinding) && iterResult.NodeResults != null)
            {
                var parts = config.SourceCompareBinding.Split('.');
                if (parts.Length >= 2)
                {
                    var binding = new ParamSetting
                    {
                        ParameterName = "源比较值",
                        BindingType = BindingType.Binding,
                        SourceNodeId = parts[0],
                        SourceProperty = parts[1],
                        TargetType = typeof(double)
                    };
                    var iterToolResults = iterResult.NodeResults
                        .Where(kvp => kvp.Value?.Result != null)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Result!);
                    var resolveResult = resolver.Resolve(binding, iterToolResults);
                    if (resolveResult.IsSuccess && resolveResult.Value != null)
                    {
                        sourceStr = resolveResult.Value.ToString();
                    }
                }
            }
            if (string.IsNullOrEmpty(sourceStr))
            {
                parameters.LogWarning("中断条件: 源比较值未配置有效绑定源，不触发中断");
                return false;
            }

            // 目的比较值：从绑定源解析（父工作流）
            string? targetStr = null;
            if (!string.IsNullOrEmpty(config.TargetCompareBinding) && nodeResults != null)
            {
                var parts = config.TargetCompareBinding.Split('.');
                if (parts.Length >= 2)
                {
                    var binding = new ParamSetting
                    {
                        ParameterName = "目的比较值",
                        BindingType = BindingType.Binding,
                        SourceNodeId = parts[0],
                        SourceProperty = parts[1],
                        TargetType = typeof(double)
                    };
                    var resolveResult = resolver.Resolve(binding, nodeResults);
                    if (resolveResult.IsSuccess && resolveResult.Value != null)
                    {
                        targetStr = resolveResult.Value.ToString();
                    }
                }
            }
            if (string.IsNullOrEmpty(targetStr))
            {
                parameters.LogWarning("中断条件: 目的比较值未配置有效绑定源，不触发中断");
                return false;
            }

            // 类型转换后比较
            if (!double.TryParse(sourceStr, out var sourceVal) || !double.TryParse(targetStr, out var targetVal))
                return false;

            bool conditionMet = config.ComparisonOperator switch
            {
                ">" => sourceVal > targetVal,
                ">=" => sourceVal >= targetVal,
                "<" => sourceVal < targetVal,
                "<=" => sourceVal <= targetVal,
                "==" => Math.Abs(sourceVal - targetVal) < 1e-9,
                "!=" => Math.Abs(sourceVal - targetVal) >= 1e-9,
                _ => false
            };

            parameters.LogInfo($"  中断判断: {sourceStr} {config.ComparisonOperator} {targetStr} = {conditionMet}");
            return conditionMet;
        }
    }
}
