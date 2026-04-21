using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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
    /// 1. 参数绑定解析
    /// 2. 执行上下文管理
    /// 3. 结果缓存
    /// 4. 错误处理
    /// 5. 日志记录注入
    /// 
    /// 使用示例：
    /// <code>
    /// var executor = new ToolExecutor(new ParameterResolver(), logger);
    /// 
    /// // 执行工具（图像通过 parameters.ResolvedInputImage 传入）
    /// parameters.ResolvedInputImage = inputImage;
    /// var result = executor.Run&lt;CircleFindResult&gt;(
    ///     tool, 
    ///     parameters, 
    ///     bindings, 
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
        /// 执行工具（泛型版本）
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <param name="bindings">参数绑定列表（可选）</param>
        /// <param name="context">执行上下文（可选）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>执行结果</returns>
        public TResult Run<TResult>(
            IToolPlugin tool,
            ToolParameters parameters,
            IList<ParamSetting>? bindings = null,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default)
            where TResult : ToolResults, new()
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var stopwatch = Stopwatch.StartNew();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: tool.ParamsType.Name.Replace("Parameters", ""),
                nodeName: nodeName ?? tool.ParamsType.Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken);

            try
            {
                // 1. 应用参数绑定
                if (bindings != null && bindings.Count > 0 && context != null)
                {
                    var applyResult = ApplyBindings(parameters, bindings, context);
                    if (!applyResult.IsSuccess)
                    {
                        parameters.LogError($"参数绑定失败: {string.Join("; ", applyResult.Errors)}");
                        return ToolResults.CreateError<TResult>(
                            $"参数绑定失败: {string.Join("; ", applyResult.Errors)}");
                    }
                }

                // 2. 执行工具（图像通过 parameters.ResolvedInputImage 获取）
                parameters.LogInfo($"开始执行工具");
                var result = tool.Run(parameters);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

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
                return ToolResults.CreateError<TResult>("执行被取消");
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
        /// <param name="bindings">参数绑定列表（可选）</param>
        /// <param name="context">执行上下文（可选）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>执行结果</returns>
        public ToolResults Run(
            IToolPlugin tool,
            ToolParameters parameters,
            IList<ParamSetting>? bindings = null,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default)
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var stopwatch = Stopwatch.StartNew();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: tool.ParamsType.Name.Replace("Parameters", ""),
                nodeName: nodeName ?? tool.ParamsType.Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken);

            try
            {
                // 1. 应用参数绑定
                if (bindings != null && bindings.Count > 0 && context != null)
                {
                    var applyResult = ApplyBindings(parameters, bindings, context);
                    if (!applyResult.IsSuccess)
                    {
                        parameters.LogError($"参数绑定失败: {string.Join("; ", applyResult.Errors)}");
                        return ToolResults.CreateError<GenericToolResults>(
                            $"参数绑定失败: {string.Join("; ", applyResult.Errors)}");
                    }
                }

                // 2. 执行工具（图像通过 parameters.ResolvedInputImage 获取）
                parameters.LogInfo($"开始执行工具");
                var result = tool.Run(parameters);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

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
                return ToolResults.CreateError<GenericToolResults>("执行被取消");
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
        /// <param name="bindings">参数绑定列表（可选）</param>
        /// <param name="context">执行上下文（可选）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>执行结果</returns>
        public TResult Run<TParams, TResult>(
            IToolPlugin<TParams, TResult> tool,
            TParams parameters,
            IList<ParamSetting>? bindings = null,
            IDictionary<string, ToolResults>? context = null,
            string? nodeName = null,
            string? nodeId = null,
            CancellationToken cancellationToken = default)
            where TParams : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            if (tool == null)
                throw new ArgumentNullException(nameof(tool));

            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var stopwatch = Stopwatch.StartNew();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: typeof(TParams).Name.Replace("Parameters", ""),
                nodeName: nodeName ?? typeof(TParams).Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken);

            try
            {
                // 1. 应用参数绑定
                if (bindings != null && bindings.Count > 0 && context != null)
                {
                    var applyResult = ApplyBindings(parameters, bindings, context);
                    if (!applyResult.IsSuccess)
                    {
                        parameters.LogError($"参数绑定失败: {string.Join("; ", applyResult.Errors)}");
                        return ToolResults.CreateError<TResult>(
                            $"参数绑定失败: {string.Join("; ", applyResult.Errors)}");
                    }
                }

                // 2. 执行工具（图像通过 parameters.ResolvedInputImage 获取）
                parameters.LogInfo($"开始执行工具");
                var result = tool.Run(parameters);

                stopwatch.Stop();
                result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;

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
                return ToolResults.CreateError<TResult>("执行被取消");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                parameters.LogError($"工具执行异常: {ex.Message}", ex);
                return ToolResults.CreateError<TResult>(ex.Message, ex);
            }
        }

        /// <summary>
        /// 应用参数绑定
        /// </summary>
        private ParameterApplyResult ApplyBindings(
            ToolParameters parameters,
            IList<ParamSetting> bindings,
            IDictionary<string, ToolResults> context)
        {
            var container = new ParamSettingContainer();
            foreach (var binding in bindings)
            {
                container.SetSetting(binding);
            }
            return _parameterResolver.ApplyToParameters(parameters, container, context);
        }
    }
}
