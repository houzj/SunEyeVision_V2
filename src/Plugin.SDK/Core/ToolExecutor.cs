using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Core
{
    // 使用别名避免与 System.Threading.ExecutionContext 冲突
    using ToolExecutionContext = SunEyeVision.Plugin.SDK.Execution.Parameters.ExecutionContext;

    /// <summary>
    /// 工具执行器 - 统一所有工具执行的唯一入口
    /// </summary>
    /// <remarks>
    /// 核心执行器，确保工具调试和工作流执行使用相同的执行路径。
    /// 
    /// 主要功能：
    /// 1. 参数绑定解析（从节点 ParamSettings 生成本次运行快照）
    /// 2. 执行上下文管理
    /// 3. 结果缓存
    /// 4. 错误处理
    /// 5. 日志记录注入
    /// 
    /// 使用示例：
    /// <code>
    /// var executor = new ToolExecutor(new ParameterResolver(), logger);
    /// 
    /// // 执行工具（图像通过 parameters.ResolvedInputImage 传入，绑定从参数内部读取）
    /// parameters.ResolvedInputImage = inputImage;
    /// var result = executor.Run&lt;CircleFindResult&gt;(
    ///     tool, 
    ///     parameters, 
    ///     context,
    ///     nodeName: "节点1");
    /// </code>
    /// </remarks>
    public partial class ToolExecutor
    {
        private readonly IParameterResolver _parameterResolver;
        private readonly ILogger? _logger;

        /// <summary>
        /// 创建工具执行器
        /// </summary>
        /// <param name="parameterResolver">参数解析器</param>
        /// <param name="logger">日志记录器（可选）</param>
        public ToolExecutor(IParameterResolver parameterResolver, ILogger? logger = null)
        {
            _parameterResolver = parameterResolver ?? throw new ArgumentNullException(nameof(parameterResolver));
            _logger = logger;
        }

        /// <summary>
        /// 按一次运行请求执行工具。
        /// </summary>
        /// <remarks>
        /// 这是新运行边界。为兼容现有 <see cref="IToolPlugin"/> 契约，执行前仍会把运行数据映射到
        /// <see cref="ToolParameters"/> 的 JsonIgnore 运行时属性；后续工具迁移可直接消费此请求。
        /// </remarks>
        public ToolResults Run(IToolPlugin tool, ToolExecutionRequest request)
        {
            if (tool == null) throw new ArgumentNullException(nameof(tool));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (tool is IAsyncToolPlugin) return ExecuteAsync(tool, request).AsTask().GetAwaiter().GetResult();
            using var inputResources = ClaimRequest(request);
            using var admission = EnterExecution(request, tool);

            ResolveRuntimeParameters(request);
            ApplyRuntimeSnapshot(request.Parameters, request.RuntimeParameters!);
            request.Parameters.ResolvedInputImage = request.ResolvedInputImage;
            request.Parameters.CommunicationInputs = ScopeCommunicationInputs(request);
            request.Parameters.CommunicationOutputs = request.CommunicationOutputs;
            request.Context = CreateExecutionContext(tool, request);
            request.Parameters.Context = request.Context;
            return RunSynchronousCore(tool, request);
        }

        /// <summary>
        /// 按一次运行请求执行强类型工具，并保留工具声明的具体结果类型。
        /// </summary>
        public TResult Run<TParameters, TResult>(
            IToolPlugin<TParameters, TResult> tool,
            ToolExecutionRequest request)
            where TParameters : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (request.Parameters is not TParameters parameters)
                throw new ArgumentException(
                    $"执行请求参数类型应为 {typeof(TParameters).Name}，实际为 {request.Parameters.GetType().Name}",
                    nameof(request));

            return (TResult)Run((IToolPlugin)tool, request);
        }

        /// <summary>
        /// 异步工具统一入口。新工具实现 <see cref="IAsyncToolPlugin"/> 时执行真正的异步逻辑，
        /// 旧工具通过同步入口兼容运行。
        /// </summary>
        public async ValueTask<ToolResults> ExecuteAsync(IToolPlugin tool, ToolExecutionRequest request)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (tool is not IAsyncToolPlugin asyncTool)
                return Run(tool, request);
            using var inputResources = ClaimRequest(request);
            using var admission = EnterExecution(request, tool);

            var parameters = request.Parameters;
            var stopwatch = Stopwatch.StartNew();

            ResolveRuntimeParameters(request);
            ApplyRuntimeSnapshot(request.Parameters, request.RuntimeParameters!);
            request.Parameters.ResolvedInputImage = request.ResolvedInputImage;
            request.Parameters.CommunicationInputs = ScopeCommunicationInputs(request);
            request.Parameters.CommunicationOutputs = request.CommunicationOutputs;

            // 异步契约的运行数据只存在于请求中，不再污染持久化参数对象。
            request.Context = CreateExecutionContext(tool, request);

            try
            {
                request.CancellationToken.ThrowIfCancellationRequested();
                // Async tools consume the request. Preparation must not attach runtime node results
                // to the configuration object that the caller supplied.
                ToolExecutionPreparation.Apply(parameters, request.NodeResults ?? new Dictionary<string, ToolResults>(), attachNodeResults: false);
                request.SetResolvedInputImage(parameters.ResolvedInputImage);
                request.Context.Logger?.Info("开始异步执行工具", request.Context.GetSourceName());
                var inputFrame = ImageFrameProvenance.CaptureInput(parameters.ResolvedInputImage);
                var result = await asyncTool.ExecuteAsync(request).ConfigureAwait(false);
                request.AttachResultMetadata(result);
                if (request.CancellationToken.IsCancellationRequested || request.ExecutionIdentity?.IsCurrent == false)
                {
                    (result as IDisposable)?.Dispose();
                    var cancelled = CreateTerminalResult(tool.ResultType, ExecutionStatus.Cancelled, "运行已取消或身份失效");
                    request.AttachResultMetadata(cancelled);
                    return cancelled;
                }
                ImageFrameProvenance.RecordResult(result, inputFrame,
                    (tool as IImageFramePolicyProvider)?.ImageFramePolicy ?? ImageFramePolicy.Unknown);
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                ApplyJudgmentStatus(parameters, result);
                return result;
            }
            catch (OperationCanceledException ex) when (request.CancellationToken.IsCancellationRequested && ex.CancellationToken == request.CancellationToken)
            {
                stopwatch.Stop();
                request.Context.Logger?.Warning("工具执行被取消", request.Context.GetSourceName());
                var cancelled = CreateTerminalResult(tool.ResultType, ExecutionStatus.Cancelled, ex.Message);
                request.AttachResultMetadata(cancelled);
                return cancelled;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                request.Context.Logger?.Error(
                    $"工具执行异常: {ex.Message}",
                    request.Context.GetSourceName(),
                    ex);
                var failed = CreateTerminalResult(tool.ResultType, ExecutionStatus.Failed, ex.Message, ex);
                request.AttachResultMetadata(failed);
                return failed;
            }
        }

        private static SunEyeVision.Plugin.SDK.Models.Communication.ICommunicationReceiveService? ScopeCommunicationInputs(ToolExecutionRequest request)
        {
            var service = request.CommunicationInputs;
            return service == null || service is SunEyeVision.Plugin.SDK.Models.Communication.RuntimeVariableReceiveService || string.IsNullOrEmpty(request.WorkflowId)
                ? service : new SunEyeVision.Plugin.SDK.Models.Communication.RuntimeVariableReceiveService(service, null, workflowId: request.WorkflowId);
        }
        private ToolExecutionContext CreateExecutionContext(IToolPlugin tool, ToolExecutionRequest request)
        {
            var toolName = tool.ParamsType.Name.Replace("Parameters", "");
            return new ToolExecutionContext(
                logger: _logger,
                toolName: toolName,
                nodeName: request.NodeName ?? toolName,
                nodeId: request.NodeId,
                cancellationToken: request.CancellationToken,
                globalVariables: request.GlobalVariables, communicationInputs: request.CommunicationInputs, communicationOutputs: request.CommunicationOutputs)
                { WorkflowId = request.WorkflowId, ResourcePaths = request.ResourcePaths, ExecutionIdentity = request.ExecutionIdentity,
                  OutputRequests = request.OutputRequests };
        }

        private static ToolResults CreateTerminalResult(
            Type resultType,
            ExecutionStatus status,
            string message,
            Exception? exception = null)
        {
            if (Activator.CreateInstance(resultType) is not ToolResults result)
                result = new GenericToolResults();

            result.Status = status;
            result.IsPass = false;
            result.ErrorMessage = message;
            result.ErrorStackTrace = exception?.StackTrace;
            result.Timestamp = DateTime.Now;
            return result;
        }

        /// <summary>
        /// 执行工具（泛型版本）
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <param name="context">上游节点结果字典（null 时使用空字典，保证工具总能安全探测）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <param name="globalVariables">本次运行的全局变量（可选）</param>
        /// <returns>执行结果</returns>
        public TResult Run<TResult>(
            IToolPlugin tool,
            ToolParameters parameters,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null)
            where TResult : ToolResults, new()
        {
            return (TResult)Run((IToolPlugin)tool, LegacyRequest(tool, parameters, context, nodeName, nodeId, cancellationToken, globalVariables));
        }

        /// <summary>
        /// 执行工具（非泛型版本）
        /// </summary>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <param name="context">上游节点结果字典（null 时使用空字典，保证工具总能安全探测）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <param name="globalVariables">本次运行的全局变量（可选）</param>
        /// <returns>执行结果</returns>
        public ToolResults Run(
            IToolPlugin tool,
            ToolParameters parameters,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null)
        {
            return Run(tool, LegacyRequest(tool, parameters, context, nodeName, nodeId, cancellationToken, globalVariables));
        }

        /// <summary>
        /// 执行工具（强类型版本）
        /// </summary>
        /// <typeparam name="TParams">参数类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <param name="context">上游节点结果字典（null 时使用空字典，保证工具总能安全探测）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <param name="globalVariables">本次运行的全局变量（可选）</param>
        /// <returns>执行结果</returns>
        public TResult Run<TParams, TResult>(
            IToolPlugin<TParams, TResult> tool,
            TParams parameters,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null)
            where TParams : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            return (TResult)Run((IToolPlugin)tool, LegacyRequest(tool, parameters, context, nodeName, nodeId, cancellationToken, globalVariables));
        }

        /// <summary>
        /// 解析本次运行的绑定快照。该路径只生成只读值，不修改设计期参数对象。
        /// 工具执行只消费解析后的 RuntimeParameters 快照，不修改设计期参数。
        /// </summary>
        public RuntimeParameterSnapshot ResolveRuntimeParameters(ToolExecutionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var inputs = BindingInputCatalog.Describe(request.Parameters);
            if (!string.IsNullOrEmpty(request.WorkflowId) && request.ParameterBindings.Bindings.Any(b => b.BindingType == BindingType.Binding &&
                !string.IsNullOrEmpty(b.SourceWorkflowId) && b.SourceNodeId?.StartsWith("__", StringComparison.Ordinal) != true && b.SourceWorkflowId != request.WorkflowId))
            {
                var invalid = new RuntimeParameterSnapshot(new Dictionary<string, object?>(), new[] { "绑定来源跨出当前工作流作用域，请使用全局变量。" });
                request.RuntimeParameters = invalid; return invalid;
            }
            foreach (var pending in request.Parameters.BindingValidationPending)
            {
                if (!BindingInputCatalog.IsInputActive(request.Parameters, pending.Key)) continue;
                var input = inputs.FirstOrDefault(i => i.Key == pending.Key);
                if (input is { Active: false }) continue;
                var invalid = new RuntimeParameterSnapshot(new Dictionary<string, object?>(), new[] { $"输入 {input?.Name ?? pending.Key} [{pending.Key}] 待校验，不能执行：{pending.Value}" });
                request.RuntimeParameters = invalid; return invalid;
            }
            foreach (var pending in request.Parameters.RebindingRequired.ToArray())
            {
                if (!BindingInputCatalog.IsInputActive(request.Parameters, pending.Key)) continue;
                var input = inputs.FirstOrDefault(i => i.Key == pending.Key);
                if (input is { Active: false }) continue;
                if (input == null && (pending.Key.StartsWith("Row:") || pending.Key.StartsWith("Part:") || pending.Key.StartsWith("Variable:"))) continue;
                var invalid = new RuntimeParameterSnapshot(new Dictionary<string, object?>(), new[] { $"输入 {input?.Name ?? pending.Key} [{pending.Key}] 需要重新绑定：{pending.Value}" });
                request.RuntimeParameters = invalid; return invalid;
            }
            if (request.ParameterBindings.Bindings.Count == 0)
            {
                var empty = new RuntimeParameterSnapshot(
                    new Dictionary<string, object?>(StringComparer.Ordinal));
                request.RuntimeParameters = empty;
                return empty;
            }

            var resolver = new RuntimeParameterSnapshotResolver(_parameterResolver);
            var targetTypes = request.Parameters.GetType()
                .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(property => property.CanWrite)
                .ToDictionary(property => property.Name, property => property.PropertyType, StringComparer.Ordinal);

            // ParamSettings 可能包含调试控件的展示绑定（例如“位置修正”）。
            // 这类名称不是 ToolParameters 的可写属性，运行时由对应的
            // AvailableDataSource/ImageSourceReference 链路处理，不能进入普通
            // 参数转换，否则会把 AffineMatrix 等对象错误地走 Convert.ChangeType。
            var executableBindings = request.ParameterBindings.Bindings
                .Where(binding => targetTypes.ContainsKey(binding.ParameterName) && BindingInputCatalog.IsInputActive(request.Parameters, binding.ParameterName))
                .ToList();
            if (executableBindings.Count != request.ParameterBindings.Bindings.Count)
            {
                _logger?.Log(
                    LogLevel.Debug,
                    $"已跳过 {request.ParameterBindings.Bindings.Count - executableBindings.Count} 个非参数绑定，交由专用数据源链路处理",
                    request.NodeName ?? nameof(ToolExecutor));
            }

            var snapshot = resolver.Resolve(
                new ParameterBindingSet(executableBindings),
                request.NodeResults ?? new Dictionary<string, ToolResults>(),
                targetTypes,request.BindingPlans,request.BindingSnapshot);
            request.RuntimeParameters = snapshot;
            return snapshot;
        }

        /// <summary>
        /// 将运行快照写入本次执行使用的参数副本。设计期参数对象不应传入此方法。
        /// </summary>
        public static void ApplyRuntimeSnapshot(ToolParameters parameters, RuntimeParameterSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(snapshot);

            if (!snapshot.IsValid)
                throw new InvalidOperationException(string.Join("; ", snapshot.Errors));

            foreach (var item in snapshot.Values)
            {
                var property = parameters.GetType().GetProperty(
                    item.Key,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (property == null || !property.CanWrite)
                    continue;

                if (item.Value == null)
                {
                    if (!property.PropertyType.IsValueType || Nullable.GetUnderlyingType(property.PropertyType) != null)
                        property.SetValue(parameters, null);
                    continue;
                }

                property.SetValue(parameters, item.Value);
            }
        }

        /// <summary>
        /// 统一把执行结果投影为业务判定结果。参数未启用任何判断项时明确为“未判定”，
        /// 不把 ToolResults.IsPass 的默认 true 误显示为 OK。
        /// </summary>
        private static void ApplyJudgmentStatus(ToolParameters parameters, ToolResults result)
        {
            if (!result.IsSuccess)
            {
                result.JudgmentStatus = "未判定";
                return;
            }

            var presentation = parameters.GetType().GetProperty("ResultPresentation")
                ?.GetValue(parameters) as ResultPresentationConfig;
            result.JudgmentStatus = presentation?.Judgments.Any(item => item.Enabled) == true
                ? (result.IsPass ? "OK" : "NG")
                : "未判定";
        }

    }
}
