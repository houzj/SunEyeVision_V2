using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
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
    /// 1. 参数绑定解析（从参数内部 BindingConfig 读取）
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
    public class ToolExecutor
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
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            request.Parameters.ResolvedInputImage = request.ResolvedInputImage;
            return Run(
                tool,
                request.Parameters,
                request.NodeResults,
                request.NodeName,
                request.NodeId,
                request.CancellationToken,
                request.GlobalVariables,
                request.ApplyBindings);
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

            var parameters = request.Parameters;
            var stopwatch = Stopwatch.StartNew();

            // 异步契约的运行数据只存在于请求中，不再污染持久化参数对象。
            request.Context = CreateExecutionContext(tool, request);

            try
            {
                if (request.NodeResults != null && request.ApplyBindings)
                {
                    var applyResult = ApplyBindings(parameters, request.NodeResults);
                    if (!applyResult.IsSuccess)
                    {
                        request.Context.Logger?.Error(
                            $"参数绑定失败: {applyResult.FailureDetail}",
                            request.Context.GetSourceName());
                        return CreateTerminalResult(
                            tool.ResultType,
                            ExecutionStatus.Failed,
                            $"参数绑定失败: {applyResult.FailureDetail}");
                    }
                }

                request.CancellationToken.ThrowIfCancellationRequested();
                request.Context.Logger?.Info("开始异步执行工具", request.Context.GetSourceName());
                var result = await asyncTool.ExecuteAsync(request).ConfigureAwait(false);
                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;
                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                request.Context.Logger?.Warning("工具执行被取消", request.Context.GetSourceName());
                return CreateTerminalResult(tool.ResultType, ExecutionStatus.Cancelled, "执行被取消");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                request.Context.Logger?.Error(
                    $"工具执行异常: {ex.Message}",
                    request.Context.GetSourceName(),
                    ex);
                return CreateTerminalResult(tool.ResultType, ExecutionStatus.Failed, ex.Message, ex);
            }
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
                globalVariables: request.GlobalVariables);
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
        /// <param name="applyBindings">是否由执行器应用参数绑定；调用方已完成绑定时传入 false</param>
        /// <returns>执行结果</returns>
        public TResult Run<TResult>(
            IToolPlugin tool,
            ToolParameters parameters,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null,
            bool applyBindings = true)
            where TResult : ToolResults, new()
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var stopwatch = Stopwatch.StartNew();

            // ★ 强制非 null：工具只需通过 TryGetValue 探测上游数据是否存在，无需判空
            parameters.NodeResults = context ?? new Dictionary<string, ToolResults>();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: tool.ParamsType.Name.Replace("Parameters", ""),
                nodeName: nodeName ?? tool.ParamsType.Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken,
                globalVariables: globalVariables);

            try
            {
                // 1. 应用参数绑定（从参数内部 BindingConfig 读取）
                if (context != null && applyBindings)
                {
                    var applyResult = ApplyBindings(parameters, context);
                    if (!applyResult.IsSuccess)
                    {
                        parameters.LogError($"参数绑定失败: {applyResult.FailureDetail}");
                        return ToolResults.CreateError<TResult>(
                            $"参数绑定失败: {applyResult.FailureDetail}");
                    }
                }

                // 2. 执行工具（图像通过 parameters.ResolvedInputImage 获取）
                parameters.LogInfo($"开始执行工具");
                var result = tool.Run(parameters);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                if (result.IsSuccess)
                {
                    parameters.LogSuccess($"工具执行成功，耗时: {stopwatch.ElapsedMilliseconds}ms");
                }
                else
                {
                    parameters.LogWarning($"工具执行失败: {result.ErrorMessage}");
                }

                return (TResult)result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                parameters.LogWarning("工具执行被取消");
                return ToolResults.CreateCancelled<TResult>();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"工具执行异常: {ex.Message}", ex);
                return ToolResults.CreateError<TResult>(ex.Message, ex);
            }
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
        /// <param name="applyBindings">是否由执行器应用参数绑定；调用方已完成绑定时传入 false</param>
        /// <returns>执行结果</returns>
        public ToolResults Run(
            IToolPlugin tool,
            ToolParameters parameters,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null,
            bool applyBindings = true)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var stopwatch = Stopwatch.StartNew();

            // ★ 强制非 null：工具只需通过 TryGetValue 探测上游数据是否存在
            parameters.NodeResults = context ?? new Dictionary<string, ToolResults>();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: tool.ParamsType.Name.Replace("Parameters", ""),
                nodeName: nodeName ?? tool.ParamsType.Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken,
                globalVariables: globalVariables);

            try
            {
                // 1. 应用参数绑定（从参数内部 BindingConfig 读取）
                if (context != null && applyBindings)
                {
                    var applyResult = ApplyBindings(parameters, context);
                    if (!applyResult.IsSuccess)
                    {
                        parameters.LogError($"参数绑定失败: {applyResult.FailureDetail}");
                        return ToolResults.CreateError<GenericToolResults>(
                            $"参数绑定失败: {applyResult.FailureDetail}");
                    }
                }

                // 2. 执行工具（图像通过 parameters.ResolvedInputImage 获取）
                parameters.LogInfo($"开始执行工具");
                var result = tool.Run(parameters);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                if (result.IsSuccess)
                {
                    parameters.LogSuccess($"工具执行成功，耗时: {stopwatch.ElapsedMilliseconds}ms");
                }
                else
                {
                    parameters.LogWarning($"工具执行失败: {result.ErrorMessage}");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                parameters.LogWarning("工具执行被取消");
                return ToolResults.CreateCancelled<GenericToolResults>();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"工具执行异常: {ex.Message}", ex);
                return ToolResults.CreateError<GenericToolResults>(ex.Message, ex);
            }
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
        /// <param name="applyBindings">是否由执行器应用参数绑定；调用方已完成绑定时传入 false</param>
        /// <returns>执行结果</returns>
        public TResult Run<TParams, TResult>(
            IToolPlugin<TParams, TResult> tool,
            TParams parameters,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default,
            IDictionary<string, object?>? globalVariables = null,
            bool applyBindings = true)
            where TParams : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var stopwatch = Stopwatch.StartNew();

            // ★ 强制非 null：工具只需通过 TryGetValue 探测上游数据是否存在
            parameters.NodeResults = context ?? new Dictionary<string, ToolResults>();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: typeof(TParams).Name.Replace("Parameters", ""),
                nodeName: nodeName ?? typeof(TParams).Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken,
                globalVariables: globalVariables);

            try
            {
                // 1. 应用参数绑定（从参数内部 BindingConfig 读取）
                if (context != null && applyBindings)
                {
                    var applyResult = ApplyBindings(parameters, context);
                    if (!applyResult.IsSuccess)
                    {
                        parameters.LogError($"参数绑定失败: {applyResult.FailureDetail}");
                        return ToolResults.CreateError<TResult>(
                            $"参数绑定失败: {applyResult.FailureDetail}");
                    }
                }

                // 2. 执行工具（图像通过 parameters.ResolvedInputImage 获取）
                parameters.LogInfo($"开始执行工具");
                var result = tool.Run(parameters);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                if (result.IsSuccess)
                {
                    parameters.LogSuccess($"工具执行成功，耗时: {stopwatch.ElapsedMilliseconds}ms");
                }
                else
                {
                    parameters.LogWarning($"工具执行失败: {result.ErrorMessage}");
                }

                return result;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                parameters.LogWarning("工具执行被取消");
                return ToolResults.CreateCancelled<TResult>();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"工具执行异常: {ex.Message}", ex);
                return ToolResults.CreateError<TResult>(ex.Message, ex);
            }
        }

        /// <summary>
        /// 应用参数绑定（从参数内部 BindingConfig 读取）
        /// </summary>
        private ParameterApplyResult ApplyBindings(
            ToolParameters parameters,
            IDictionary<string, ToolResults> context)
        {
            return _parameterResolver.ApplyBindings(parameters, context);
        }

    }
}
