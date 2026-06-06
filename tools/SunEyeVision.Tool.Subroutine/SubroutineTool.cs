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
        /// 工作流引擎引用（框架初始化时注入）
        /// </summary>
        public static WorkflowEngine? WorkflowEngine { get; set; }

        /// <summary>
        /// 工作流执行引擎引用（框架初始化时注入）
        /// </summary>
        public static WorkflowExecutionEngine? WorkflowExecutionEngine { get; set; }

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
                if (WorkflowEngine == null)
                    throw new InvalidOperationException("工作流引擎未初始化，请检查框架初始顺序");

                if (WorkflowExecutionEngine == null)
                    throw new InvalidOperationException("工作流执行引擎未初始化，请检查框架初始顺序");

                if (string.IsNullOrEmpty(parameters.SubroutineId))
                    throw new InvalidOperationException($"子程序 \"{parameters.SubroutineName}\" 未配置目标工作流");

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
            if (WorkflowExecutionEngine == null)
                throw new InvalidOperationException("工作流执行引擎未初始化，请检查框架初始顺序");

            return Task.Run(async () =>
                await WorkflowExecutionEngine.ExecuteWorkflowAsync(parameters.SubroutineId, inputImage, null, contextVariables))
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

            for (int i = 0; i < config.MaxIterations; i++)
            {
                parameters.CurrentIteration = i;
                var iterResult = ExecuteSubWorkflowOnce(parameters, inputImage, contextVariables);
                result.Merge(iterResult);
                if (!iterResult.Success)
                    break;
            }

            return result;
        }
    }
}
