using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Visualization;
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

                    // 1. 解析输入图像：优先使用参数绑定中的 ImageSource
                    var inputImage = ResolveInputImage(typedParams, nodeResults);
                    typedParams.ResolvedInputImage = inputImage;
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[DIAG] {Name}({Id}): ResolvedInputImage={(inputImage == null ? "null" : $"{inputImage.GetType().Name}, IsEmpty={inputImage.IsEmpty}")}",
                        $"诊断.算法节点");

                    // 参数绑定统一由节点 ParamSettings 构造运行快照，在 ToolExecutor 中注入运行副本。
                    // 设计期参数对象不再读取或写入 旧包装器 内部状态。
                    var resolver = new ParameterResolver(VisionLogger.Instance);

                    // 设计参数只保存配置；执行使用独立副本，外部 ParamSettings 解析结果进入运行快照。
                    var executionParams = CreateExecutionParameters(typedParams);

                    // 2a. 刷新所有 AvailableDataSource 属性的运行时 CurrentValue
                    if (nodeResults != null)
                    {
                        ResolveAvailableDataSources(executionParams, nodeResults);
                    }

                    // 3a. 解析参数中所有 TextStyle 的运行时绑定（工具执行前解析，使 PositionX/Y 绑定值在工具中可用）
                    if (nodeResults != null)
                    {
                        ResolveTextStyleParameterBindings(executionParams, nodeResults, resolver);
                    }

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
        /// 解析输入图像 — 从参数中的 ImageSource 绑定获取
        /// </summary>
        /// <remarks>
        /// 返回 null 表示节点为图像源节点（如 ImageLoad），自行从文件加载图像。
        /// 下游工具如需要输入图像会自行检查 null 并报错。
        /// </remarks>
        private IVisionImage? ResolveInputImage(ToolParameters parameters, IDictionary<string, ToolResults>? nodeResults)
        {
            var logger = VisionLogger.Instance;
            var paramTypeName = parameters.GetType().Name;

            // 参数中的 ImageSource 绑定（AvailableDataSource）
            var imageSourceProp = parameters.GetType().GetProperty("ImageSource");
            if (imageSourceProp != null)
            {
                var imageSource = imageSourceProp.GetValue(parameters) as AvailableDataSource;
                logger.Log(LogLevel.Info,
                    $"[DIAG] {Name}({Id}): ResolveInputImage, paramType={paramTypeName}, ImageSource={(imageSource == null ? "null" : $"SourceNodeId={imageSource.SourceNodeId}, PropertyName={imageSource.PropertyName}")}, nodeResults={(nodeResults == null ? "null" : $"Count={nodeResults.Count}")}",
                    $"诊断.算法节点");

                if (imageSource != null && nodeResults != null)
                {
                    if (nodeResults.TryGetValue(imageSource.SourceNodeId, out var sourceResult))
                    {
                        var value = GetPropertyValueFromResult(sourceResult, imageSource.PropertyName);
                        logger.Log(LogLevel.Info,
                            $"[DIAG] {Name}({Id}): nodeResults contains SourceNodeId={imageSource.SourceNodeId}, GetProperty={imageSource.PropertyName}, value type={(value?.GetType().Name ?? "null")}, is IVisionImage={(value is IVisionImage)}",
                            $"诊断.算法节点");
                        if (value is IVisionImage image && !image.IsEmpty)
                        {
                            return image;
                        }
                    }
                    else
                    {
                        logger.Log(LogLevel.Info,
                            $"[DIAG] {Name}({Id}): nodeResults does NOT contain SourceNodeId={imageSource.SourceNodeId}, available keys=[{string.Join(", ", nodeResults.Keys)}]",
                            $"诊断.算法节点");
                    }
                }

                // 如果 ImageSource 为 null，尝试从 ImageSourceRef 恢复
                if (imageSource == null)
                {
                    var restoredSource = RestoreAvailableDataSourceFromRef(parameters, imageSourceProp);
                    if (restoredSource != null)
                    {
                        logger.Log(LogLevel.Info,
                            $"[DIAG] {Name}({Id}): ImageSource was null, restored from ImageSourceRef: SourceNodeId={restoredSource.SourceNodeId}, PropertyName={restoredSource.PropertyName}",
                            $"诊断.算法节点");

                        // 重试查找
                        if (nodeResults != null && nodeResults.TryGetValue(restoredSource.SourceNodeId, out var sourceResult2))
                        {
                            var value = GetPropertyValueFromResult(sourceResult2, restoredSource.PropertyName);
                            if (value is IVisionImage image && !image.IsEmpty)
                            {
                                return image;
                            }
                        }
                    }
                    else
                    {
                        logger.Log(LogLevel.Info,
                            $"[DIAG] {Name}({Id}): ImageSource was null, no ImageSourceRef found to restore from",
                            $"诊断.算法节点");
                    }
                }
            }
            else
            {
                logger.Log(LogLevel.Info,
                    $"[DIAG] {Name}({Id}): ResolveInputImage, paramType={paramTypeName}, ImageSource property NOT FOUND",
                    $"诊断.算法节点");
            }

            // 无有效输入图像源 → 返回 null（起始节点自行加载图像）
            return null;
        }

        /// <summary>
        /// 从工具结果中获取指定属性的值（通过输出变量契约读取，无反射、无 GetResultItems 兜底）
        /// </summary>
        private object? GetPropertyValueFromResult(ToolResults result, string propertyName)
        {
            return OutputVariableResolver.Resolve(result, propertyName);
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
                return AlgorithmResult.CreateError(result.ErrorMessage ?? "处理失败");
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

        /// <summary>
        /// 解析工具结果中所有 TextStyle 的运行时绑定值
        /// </summary>
        /// <remarks>
        /// 遍历 DisplayCollector 中的 TextDisplayItem，对其 TextStyle 调用 ResolveBindings，
        /// 使 PositionX/PositionY 等绑定属性的来源节点值在渲染时生效。
        /// </remarks>
        /// <summary>
        /// 解析参数对象中所有 TextStyle 属性的运行时绑定值（在工具执行前调用）
        /// </summary>
        private static void ResolveTextStyleParameterBindings(ToolParameters parameters, IDictionary<string, ToolResults> nodeResults, ParameterResolver resolver)
        {
            foreach (var prop in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType == typeof(TextStyle) && prop.GetValue(parameters) is TextStyle style)
                {
                    // TextStyle 继承 ObservableObject，SetProperty 会触发 PropertyChanged 事件，
                    // WPF 数据绑定监听此事件但必须在 UI 线程上处理，因此需要调度到 UI 线程执行
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        style.ResolveBindings(nodeResults, resolver);
                    });
                }
            }
        }

        /// <summary>
        /// 刷新参数对象中所有 AvailableDataSource 属性的运行时 CurrentValue。
        /// 在设计时 AvailableDataSource 的 CurrentValue 为 null（上游节点未执行），
        /// 此方法在工具执行前从 nodeResults 中读取实际值并回填。
        /// 
        /// 如果 AvailableDataSource 属性为 null（因 [JsonIgnore] 反序列化后为空），
        /// 会自动从对应的 ImageSourceReference 属性恢复（如 PositionCorrectionBindingSource → PositionCorrectionSource）。
        /// </summary>
        private void ResolveAvailableDataSources(
            ToolParameters parameters,
            IDictionary<string, ToolResults> nodeResults)
        {
            var logger = VisionLogger.Instance;

            foreach (var prop in parameters.GetType()
                .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.PropertyType != typeof(AvailableDataSource))
                    continue;

                var dataSource = prop.GetValue(parameters) as AvailableDataSource;
                var restored = false;

                // 如果 AvailableDataSource 为 null（[JsonIgnore] 反序列化后丢失），
                // 从对应的 ImageSourceReference 属性恢复
                if (dataSource == null)
                {
                    dataSource = RestoreAvailableDataSourceFromRef(parameters, prop);
                    if (dataSource == null)
                    {
                        logger.Log(LogLevel.Info,
                            $"[DIAG] {Name}({Id}): ResolveAvailableDataSources property={prop.Name} is null, cannot restore from ref",
                            $"诊断.算法节点");
                        continue;
                    }
                    restored = true;
                }

                if (string.IsNullOrEmpty(dataSource.SourceNodeId))
                {
                    if (restored)
                        logger.Log(LogLevel.Info,
                            $"[DIAG] {Name}({Id}): ResolveAvailableDataSources property={prop.Name} restored but SourceNodeId is empty",
                            $"诊断.算法节点");
                    continue;
                }
                if (string.IsNullOrEmpty(dataSource.PropertyName))
                    continue;

                if (restored)
                {
                    logger.Log(LogLevel.Info,
                        $"[DIAG] {Name}({Id}): ResolveAvailableDataSources property={prop.Name} restored from ref, SourceNodeId={dataSource.SourceNodeId}, PropertyName={dataSource.PropertyName}",
                        $"诊断.算法节点");
                }

                if (!nodeResults.TryGetValue(dataSource.SourceNodeId, out var sourceResult))
                {
                    logger.Log(LogLevel.Info,
                        $"[DIAG] {Name}({Id}): ResolveAvailableDataSources property={prop.Name}, SourceNodeId={dataSource.SourceNodeId} NOT found in nodeResults, keys=[{string.Join(", ", nodeResults.Keys)}]",
                        $"诊断.算法节点");
                    continue;
                }

                var value = GetPropertyValueFromResult(sourceResult, dataSource.PropertyName);
                if (value != null)
                {
                    dataSource.CurrentValue = value;
                    logger.Log(LogLevel.Info,
                        $"[DIAG] {Name}({Id}): ResolveAvailableDataSources property={prop.Name}, SourceNodeId={dataSource.SourceNodeId}, CurrentValue set, type={value.GetType().Name}",
                        $"诊断.算法节点");
                }
                else
                {
                    logger.Log(LogLevel.Info,
                        $"[DIAG] {Name}({Id}): ResolveAvailableDataSources property={prop.Name}, SourceNodeId={dataSource.SourceNodeId}, GetPropertyValueFromResult returned null",
                        $"诊断.算法节点");
                }
            }
        }

        /// <summary>
        /// 从对应的 ImageSourceReference 属性恢复 AvailableDataSource。
        /// 命名约定：xxxBindingSource ↔ xxxSource / xxx ↔ xxxRef
        /// </summary>
        private static AvailableDataSource? RestoreAvailableDataSourceFromRef(
            ToolParameters parameters,
            PropertyInfo availableDataSourceProp)
        {
            var propName = availableDataSourceProp.Name;

            // 尝试多种命名模式查找对应的 ImageSourceReference 属性
            string[] candidateRefNames;

            if (propName.EndsWith("BindingSource"))
            {
                var baseName = propName[..^"BindingSource".Length];
                candidateRefNames = new[] { baseName + "Source", baseName + "Ref", baseName + "Reference" };
            }
            else
            {
                candidateRefNames = new[] { propName + "Ref", propName + "Reference", propName + "Source" };
            }

            foreach (var refName in candidateRefNames)
            {
                var refProp = parameters.GetType().GetProperty(refName, BindingFlags.Public | BindingFlags.Instance);
                if (refProp == null || refProp.PropertyType != typeof(ImageSourceReference))
                    continue;

                var refValue = refProp.GetValue(parameters) as ImageSourceReference;
                if (refValue == null || string.IsNullOrEmpty(refValue.SourceNodeId))
                    continue;

                // 直接从 ImageSourceReference 创建 AvailableDataSource
                var dataSource = new AvailableDataSource
                {
                    SourceNodeId = refValue.SourceNodeId,
                    SourceWorkflowId = refValue.SourceWorkflowId, SourceFieldId = refValue.SourceFieldId,
                    AutomaticConnectionId = refValue.AutomaticConnectionId,
                    SourceNodeName = refValue.SourceNodeName ?? "",
                    PropertyName = refValue.PropertyName ?? "",
                    DisplayName = refValue.DisplayName ?? "",
                };

                // 设置回原属性（供后续代码使用）
                availableDataSourceProp.SetValue(parameters, dataSource);
                return dataSource;
            }

            return null;
        }

    }
}
