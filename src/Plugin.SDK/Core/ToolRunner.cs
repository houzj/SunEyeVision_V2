using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具运行器 - 用于单工具调试场景
    /// </summary>
    /// <remarks>
    /// 单工具运行器，提供调试场景下的工具执行能力。
    /// 使用统一的 ToolExecutor 确保与工作流执行路径一致。
    /// 
    /// 主要功能：
    /// 1. 同步/异步执行工具
    /// 2. 支持取消操作
    /// 3. 执行状态跟踪
    /// 4. 结果缓存
    /// 
    /// 使用示例：
    /// <code>
    /// var runner = new ToolRunner(executor);
    /// parameters.ResolvedInputImage = inputImage; // 设置输入图像
    /// var result = await runner.RunAsync(tool, parameters);
    /// if (result.IsSuccess)
    /// {
    ///     // 处理执行结果，如 result.ExecutionTimeMs 耗时
    /// }
    /// </code>
    /// </remarks>
    public class ToolRunner
    {
        private readonly ToolExecutor _executor;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning;

        /// <summary>
        /// 当前是否正在运行
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// 执行完成事件
        /// </summary>
        public event EventHandler<RunResult>? RunCompleted;

        /// <summary>
        /// 创建工具运行器
        /// </summary>
        /// <param name="executor">工具执行器</param>
        public ToolRunner(ToolExecutor executor)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        }

        /// <summary>
        /// 同步运行工具
        /// </summary>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <returns>运行结果</returns>
        public RunResult Run(IToolPlugin tool, ToolParameters parameters)
        {
            if (_isRunning)
            {
                return RunResult.Failure("工具正在运行中");
            }

            _isRunning = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 使用统一执行器执行工具
                var toolResult = _executor.Run(tool, parameters);
                stopwatch.Stop();

                var result = RunResult.FromToolResults(toolResult);
                result.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                RunCompleted?.Invoke(this, result);
                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return RunResult.Failure(ex.Message, ex);
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// 异步运行工具
        /// </summary>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <returns>运行结果</returns>
        public async Task<RunResult> RunAsync(IToolPlugin tool, ToolParameters parameters)
        {
            if (_isRunning)
            {
                return RunResult.Failure("工具正在运行中");
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return _executor.Run(tool, parameters, cancellationToken: cancellationToken);
                }, cancellationToken);

                stopwatch.Stop();

                var runResult = RunResult.FromToolResults(result);
                runResult.ExecutionTimeMs = stopwatch.Elapsed.TotalMilliseconds;

                RunCompleted?.Invoke(this, runResult);
                return runResult;
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return RunResult.Cancelled();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return RunResult.Failure(ex.Message, ex);
            }
            finally
            {
                _isRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 运行工具（泛型版本）
        /// </summary>
        /// <typeparam name="TParams">参数类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <returns>运行结果</returns>
        public RunResult<TResult> Run<TParams, TResult>(
            IToolPlugin<TParams, TResult> tool,
            TParams parameters)
            where TParams : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            if (_isRunning)
            {
                return RunResult<TResult>.Failure("工具正在运行中");
            }

            _isRunning = true;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 使用统一执行器执行工具
                var toolResult = _executor.Run(tool, parameters);
                stopwatch.Stop();

                return RunResult<TResult>.FromToolResults(toolResult);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return RunResult<TResult>.Failure(ex.Message, ex);
            }
            finally
            {
                _isRunning = false;
            }
        }

        /// <summary>
        /// 异步运行工具（泛型版本）
        /// </summary>
        /// <typeparam name="TParams">参数类型</typeparam>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数（图像通过 ResolvedInputImage 传入）</param>
        /// <returns>运行结果</returns>
        public async Task<RunResult<TResult>> RunAsync<TParams, TResult>(
            IToolPlugin<TParams, TResult> tool,
            TParams parameters)
            where TParams : ToolParameters, new()
            where TResult : ToolResults, new()
        {
            if (_isRunning)
            {
                return RunResult<TResult>.Failure("工具正在运行中");
            }

            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var result = await Task.Run(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return _executor.Run(tool, parameters, cancellationToken: cancellationToken);
                }, cancellationToken);

                stopwatch.Stop();
                return RunResult<TResult>.FromToolResults(result);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return RunResult<TResult>.Cancelled();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return RunResult<TResult>.Failure(ex.Message, ex);
            }
            finally
            {
                _isRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        /// <summary>
        /// 取消当前执行
        /// </summary>
        public void Cancel()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="tool">工具实例</param>
        /// <param name="parameters">参数</param>
        /// <returns>验证结果</returns>
        public bool Validate(IToolPlugin tool, ToolParameters parameters)
        {
            if (parameters == null)
                return false;
            return parameters.Validate().IsValid;
        }
    }
}
