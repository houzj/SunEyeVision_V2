using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 算法节点
    /// </summary>
    public class AlgorithmNode : WorkflowNodeBase
    {
        /// <summary>
        /// 工具实例
        /// </summary>
        public IToolPlugin? Tool { get; set; }

        /// <summary>运行请求提供的共享采集服务，不属于节点持久化状态。</summary>
        public ICameraCaptureService? CameraCaptureService { get; set; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? CommunicationInputs { get; set; }
        public SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationSendService? CommunicationOutputs { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string? ExecutionWorkflowId { get; set; }

        /// <summary>本轮运行在启动节点前绑定的帧；节点不拥有其生命周期。</summary>
        public CameraFrame? CameraFrame { get; set; }

        /// <summary>
        /// 上次执行结果
        /// </summary>
        public AlgorithmResult LastResult { get; private set; }

        /// <summary>
        /// 上次工具执行结果（保留原始完整结果）
        /// </summary>
        public ToolResults? LastToolResult { get; private set; }

        public AlgorithmNode(string id, string name, string dispName, IToolPlugin tool)
            : base(id, name, dispName, "Algorithm")
        {
            Tool = tool;
        }

        /// <summary>
        /// 执行节点算法（带绑定解析）
        /// </summary>
        /// <param name="nodeResults">其他节点的执行结果缓存（用于动态绑定解析）</param>
        /// <param name="globalVariables">本次工作流运行的全局变量</param>
        /// <param name="cancellationToken">本次工作流运行的取消令牌</param>
        public AlgorithmResult Execute(
            IDictionary<string, ToolResults>? nodeResults,
            IDictionary<string, object?>? globalVariables = null,
            CancellationToken cancellationToken = default)
            => ExecuteAsync(nodeResults, globalVariables, cancellationToken).GetAwaiter().GetResult();

        /// <summary>
        /// 异步执行节点。工作流主链使用此入口，使异步工具不会被同步阻塞。
        /// </summary>
        public async Task<AlgorithmResult> ExecuteAsync(
            IDictionary<string, ToolResults>? nodeResults,
            IDictionary<string, object?>? globalVariables = null,
            CancellationToken cancellationToken = default)
        {
            if (!IsEnabled)
            {
                return AlgorithmResult.CreateSuccess(
                    null,
                    0);
            }

            OnBeforeExecute();

            try
            {
                if (Tool != null)
                {
                    var typedParams = GetTypedParameters();

                    // Shared executor prepares image, source values, correction and text bindings.
                    var executionParams = CreateExecutionParameters(typedParams);
                    var inputImage = ToolExecutionPreparation.ResolveInputImage(executionParams,
                        nodeResults ?? new Dictionary<string, ToolResults>(), null);
                    var resolver = new ParameterResolver(VisionLogger.Instance);

                    // ★ 通过 ToolExecutor 统一执行路径（与工具调试共享同一执行器）
                    // ToolExecutor 负责：注入 ExecutionContext（含 WorkflowName/GlobalVariables）、
                    // 强制 NodeResults 非空、捕获异常、设置 ExecutionTimeMs
                    var executor = new ToolExecutor(resolver, VisionLogger.Instance);
                    var toolResult = await executor.ExecuteAsync(
                        Tool,
                        new ToolExecutionRequest(executionParams)
                        {
                            NodeResults = nodeResults,
                            ResolvedInputImage = inputImage,
                            NodeName = Name,
                            NodeId = Id,
                            WorkflowId = ExecutionWorkflowId,
                            CancellationToken = cancellationToken,
                            CameraCaptureService = CameraCaptureService,
                            CommunicationInputs = CommunicationInputs,
                            CommunicationOutputs = CommunicationOutputs,
                            CameraFrame = CameraFrame,
                            GlobalVariables = globalVariables,
                            ParameterBindings = ParameterBindingSet.FromSettings(ParamSettings)
                        }).ConfigureAwait(false);

                    // Stateful calibration points are committed only after execution, never
                    // by mutating the parameter snapshot seen by the tool. A stale concurrent
                    // capture is rejected by the parameter implementation.
                    if (typedParams is IWorkflowResultCommitter committer
                        && toolResult.Status != ExecutionStatus.Failed
                        && !committer.TryCommit(toolResult, out var commitError))
                    {
                        toolResult.SetError(commitError ?? "标定点保存失败");
                    }

                    if (toolResult.Status == ExecutionStatus.Failed && !string.IsNullOrWhiteSpace(toolResult.ErrorMessage))
                    {
                        LastToolResult = toolResult;
                        LastResult = ConvertToAlgorithmResult(toolResult);
                        OnAfterExecute(LastResult);
                        return LastResult;
                    }

                    LastToolResult = toolResult;
                    LastResult = ConvertToAlgorithmResult(toolResult);
                }
                else
                {
                    LastResult = AlgorithmResult.CreateError("节点未配置工具实例");
                }

                OnAfterExecute(LastResult);
                return LastResult;
            }
            catch (OperationCanceledException)
            {
                var result = AlgorithmResult.CreateCancelled();
                OnAfterExecute(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = AlgorithmResult.CreateError($"节点 {Name} 执行失败: {ex.Message}");
                OnAfterExecute(result);
                return result;
            }
        }



        /// <summary>
        /// 获取强类型参数
        /// </summary>
        /// <remarks>
        /// 直接返回节点的 Parameters 属性，避免反射转换开销。
        /// 参数已由工作流引擎在创建节点时设置正确的类型。
        /// </remarks>
        private ToolParameters GetTypedParameters()
        {
            // 如果参数已经是正确的类型，直接返回
            if (Tool != null && Parameters.GetType() == Tool.ParamsType)
                return Parameters;

            // ✅ 如果参数类型不匹配，直接抛出异常
            throw new InvalidOperationException(
                $"参数类型不匹配。工具 {ToolType} 期望 {Tool.ParamsType?.Name ?? "null"}, 实际为 {Parameters.GetType().Name}");
        }

        private static ToolParameters CreateExecutionParameters(ToolParameters designParameters)
        {
            if (designParameters.GetType().GetConstructor(Type.EmptyTypes) == null)
                return designParameters;

            if (Activator.CreateInstance(designParameters.GetType()) is not ToolParameters runtimeParameters)
                return designParameters;

            runtimeParameters.CopyFrom(designParameters);
            return runtimeParameters;
        }

        /// <summary>
        /// 将 ToolResults 转换为 AlgorithmResult
        /// </summary>
        private AlgorithmResult ConvertToAlgorithmResult(ToolResults result)
        {
            if (result.Status == ExecutionStatus.Cancelled)
            {
                var cancelled = AlgorithmResult.CreateCancelled(result.ErrorMessage ?? "执行被取消");
                cancelled.ToolResults = result;
                return cancelled;
            }

            if (!result.IsSuccess)
            {
                var failed = AlgorithmResult.CreateError(result.ErrorMessage ?? "处理失败");
                // Retain this execution's diagnostic payload without changing failure semantics.
                failed.ToolResults = result;
                return failed;
            }

            // 获取第一张可显示图像作为主输出
            var displayImages = result.GetDisplayImages();
            var primaryImage = displayImages.Values.FirstOrDefault(m => m != null && !m.IsEmpty);

            // 如果没有输出图像，返回成功（无主图像的结果仍然可以包含结构化数据）
            if (primaryImage == null)
            {
                var nullResult = AlgorithmResult.CreateSuccess(null, result.ExecutionTimeMs);
                nullResult.ToolResults = result;
                return nullResult;
            }

            var algorithmResult = AlgorithmResult.CreateSuccess(primaryImage, result.ExecutionTimeMs);

            // 保留原始工具结果引用
            algorithmResult.ToolResults = result;

            return algorithmResult;
        }



    }
}
