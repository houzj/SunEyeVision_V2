using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
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
        /// 执行工具（泛型版本）
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <param name="context">上游节点结果字典（null 时使用空字典，保证工具总能安全探测）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>执行结果</returns>
        public TResult Run<TResult>(
            IToolPlugin tool,
            ToolParameters parameters,
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

            // ★ 强制非 null：工具只需通过 TryGetValue 探测上游数据是否存在，无需判空
            parameters.NodeResults = context ?? new Dictionary<string, ToolResults>();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: tool.ParamsType.Name.Replace("Parameters", ""),
                nodeName: nodeName ?? tool.ParamsType.Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken,
                globalVariables: TryGetGlobalVariables());

            try
            {
                // 1. 应用参数绑定（从参数内部 BindingConfig 读取）
                if (context != null)
                {
                    var applyResult = ApplyBindings(parameters, context);
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
        /// <param name="context">上游节点结果字典（null 时使用空字典，保证工具总能安全探测）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>执行结果</returns>
        public ToolResults Run(
            IToolPlugin tool,
            ToolParameters parameters,
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

            // ★ 强制非 null：工具只需通过 TryGetValue 探测上游数据是否存在
            parameters.NodeResults = context ?? new Dictionary<string, ToolResults>();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: tool.ParamsType.Name.Replace("Parameters", ""),
                nodeName: nodeName ?? tool.ParamsType.Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken,
                globalVariables: TryGetGlobalVariables());

            try
            {
                // 1. 应用参数绑定（从参数内部 BindingConfig 读取）
                if (context != null)
                {
                    var applyResult = ApplyBindings(parameters, context);
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
        /// <param name="context">上游节点结果字典（null 时使用空字典，保证工具总能安全探测）</param>
        /// <param name="nodeName">节点名称（可选，用于日志来源）</param>
        /// <param name="nodeId">节点ID（可选）</param>
        /// <param name="cancellationToken">取消令牌（可选）</param>
        /// <returns>执行结果</returns>
        public TResult Run<TParams, TResult>(
            IToolPlugin<TParams, TResult> tool,
            TParams parameters,
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

            // ★ 强制非 null：工具只需通过 TryGetValue 探测上游数据是否存在
            parameters.NodeResults = context ?? new Dictionary<string, ToolResults>();

            // 注入执行上下文
            parameters.Context = new ToolExecutionContext(
                logger: _logger,
                toolName: typeof(TParams).Name.Replace("Parameters", ""),
                nodeName: nodeName ?? typeof(TParams).Name.Replace("Parameters", ""),
                nodeId: nodeId,
                cancellationToken: cancellationToken,
                globalVariables: TryGetGlobalVariables());

            try
            {
                // 1. 应用参数绑定（从参数内部 BindingConfig 读取）
                if (context != null)
                {
                    var applyResult = ApplyBindings(parameters, context);
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
        /// 应用参数绑定（从参数内部 BindingConfig 读取）
        /// </summary>
        private ParameterApplyResult ApplyBindings(
            ToolParameters parameters,
            IDictionary<string, ToolResults> context)
        {
            return _parameterResolver.ApplyBindings(parameters, context);
        }

        /// <summary>
        /// 尝试从解决方案管理器获取全局变量字典（反射访问避免跨层耦合）
        /// </summary>
        /// <remarks>
        /// 工具层不能直接引用 SunEyeVision.UI 程序集（分层架构约束），
        /// 通过反射访问 SunEyeVision.UI.Adapters.ServiceInitializer.SolutionManager.CurrentSolution.GlobalVariables。
        /// 反射失败（UI 未加载、解决方案未初始化等）→ 静默返回 null。
        /// </remarks>
        /// <returns>全局变量字典，若无法获取返回 null</returns>
        private static IDictionary<string, object?>? TryGetGlobalVariables()
        {
            try
            {
                // 查找 SunEyeVision.UI.Adapters.ServiceInitializer 类型
                var serviceInitializerType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("SunEyeVision.UI.Adapters.ServiceInitializer"))
                    .FirstOrDefault(t => t != null);
                if (serviceInitializerType == null) return null;

                // 读取静态属性 SolutionManager
                var solutionManagerProp = serviceInitializerType.GetProperty(
                    "SolutionManager",
                    BindingFlags.Public | BindingFlags.Static);
                if (solutionManagerProp == null) return null;

                var solutionManager = solutionManagerProp.GetValue(null);
                if (solutionManager == null) return null;

                // 读取实例属性 CurrentSolution
                var currentSolutionProp = solutionManager.GetType().GetProperty(
                    "CurrentSolution",
                    BindingFlags.Public | BindingFlags.Instance);
                if (currentSolutionProp == null) return null;

                var currentSolution = currentSolutionProp.GetValue(solutionManager);
                if (currentSolution == null) return null;

                // 读取 GlobalVariables 列表（List<GlobalVariable>）
                var globalVariablesProp = currentSolution.GetType().GetProperty(
                    "GlobalVariables",
                    BindingFlags.Public | BindingFlags.Instance);
                if (globalVariablesProp == null) return null;

                var globalVariablesList = globalVariablesProp.GetValue(currentSolution) as IEnumerable;
                if (globalVariablesList == null) return null;

                // 反射遍历每个 GlobalVariable：取 Name 和 Value
                var dict = new Dictionary<string, object?>();
                foreach (var v in globalVariablesList)
                {
                    if (v == null) continue;
                    var vType = v.GetType();
                    var nameProp = vType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                    var valueProp = vType.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
                    if (nameProp == null || valueProp == null) continue;
                    var name = nameProp.GetValue(v) as string;
                    if (string.IsNullOrEmpty(name)) continue;
                    dict[name] = valueProp.GetValue(v);
                }
                return dict;
            }
            catch
            {
                // 反射失败（UI 层未加载、初始化异常等）→ 返回 null
                // Script 工具解析时若发现 BindingSource = "__GlobalVars__.xxx" 而 GlobalVariables 为 null → 会直接报错
                return null;
            }
        }
    }
}
