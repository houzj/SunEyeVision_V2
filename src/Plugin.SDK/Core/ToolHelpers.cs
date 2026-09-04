using System;
using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具辅助方法 - 按需调用
    /// </summary>
    /// <remarks>
    /// 提供常用的辅助方法，开发者可选择使用或不使用。
    /// 这些方法不会强制任何执行流程，完全由开发者决定是否调用。
    /// 
    /// 使用示例：
    /// <code>
    /// public ThresholdResult Run(Mat image, ThresholdParams parameters)
    /// {
    ///     // 验证输入
    ///     if (!ToolHelpers.ValidateInput(image, out var error))
    ///         return ToolHelpers.Error&lt;ThresholdResult&gt;(error);
    ///     
    ///     // 验证参数
    ///     if (!ToolHelpers.ValidateParameters(parameters, out error))
    ///         return ToolHelpers.Error&lt;ThresholdResult&gt;(error);
    ///     
    ///     var sw = ToolHelpers.StartTimer();
    ///     
    ///     // 执行算法
    ///     var result = new ThresholdResult();
    ///     Cv2.Threshold(image, result.OutputImage, parameters.Threshold, 255, ThresholdType.Binary);
    ///     
    ///     ToolHelpers.StopTimer(sw, result);
    ///     ToolHelpers.Success(result);
    ///     return result;
    /// }
    /// </code>
    /// </remarks>
    public static class ToolHelpers
    {
        #region 输入验证

        /// <summary>
        /// 验证输入图像
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <returns>是否有效</returns>
        public static bool ValidateInput(IVisionImage? image)
        {
            return image != null && !image.IsEmpty;
        }

        /// <summary>
        /// 验证输入图像（带错误信息）
        /// </summary>
        /// <param name="image">输入图像</param>
        /// <param name="error">错误信息</param>
        /// <returns>是否有效</returns>
        public static bool ValidateInput(IVisionImage? image, out string? error)
        {
            if (image == null || image.IsEmpty)
            {
                error = "输入图像为空";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// 验证参数
        /// </summary>
        /// <param name="parameters">参数</param>
        /// <param name="error">错误信息</param>
        /// <returns>是否有效</returns>
        public static bool ValidateParameters(ToolParameters? parameters, out string? error)
        {
            if (parameters == null)
            {
                error = "参数不能为空";
                return false;
            }

            var result = parameters.Validate();
            if (!result.IsValid)
            {
                error = string.Join("; ", result.Errors);
                return false;
            }

            error = null;
            return true;
        }

        #endregion

        #region 结果创建

        /// <summary>
        /// 创建错误结果
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="message">错误信息</param>
        /// <param name="exception">异常（可选）</param>
        /// <returns>错误结果</returns>
        public static T Error<T>(string message, Exception? exception = null)
            where T : ToolResults, new()
        {
            return ToolResults.CreateError<T>(message, exception);
        }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        /// <typeparam name="T">结果类型</typeparam>
        /// <param name="executionTimeMs">执行时间（毫秒）</param>
        /// <returns>成功结果</returns>
        public static T Success<T>(long executionTimeMs = 0)
            where T : ToolResults, new()
        {
            var result = new T();
            result.SetSuccess(executionTimeMs);
            return result;
        }

        /// <summary>
        /// 设置成功状态
        /// </summary>
        /// <param name="result">结果对象</param>
        /// <param name="executionTimeMs">执行时间（毫秒）</param>
        public static void Success(ToolResults result, long executionTimeMs = 0)
        {
            result.SetSuccess(executionTimeMs);
        }

        /// <summary>
        /// 设置错误状态
        /// </summary>
        /// <param name="result">结果对象</param>
        /// <param name="message">错误信息</param>
        /// <param name="exception">异常（可选）</param>
        public static void Error(ToolResults result, string message, Exception? exception = null)
        {
            result.SetError(message, exception);
        }

        #endregion

        #region 执行计时

        /// <summary>
        /// 创建并启动计时器
        /// </summary>
        /// <returns>计时器</returns>
        public static Stopwatch StartTimer()
        {
            var sw = new Stopwatch();
            sw.Start();
            return sw;
        }

        /// <summary>
        /// 停止计时并设置执行时间
        /// </summary>
        /// <param name="stopwatch">计时器</param>
        /// <param name="result">结果对象</param>
        public static void StopTimer(Stopwatch stopwatch, ToolResults result)
        {
            stopwatch.Stop();
            result.ExecutionTimeMs = stopwatch.ElapsedMilliseconds;
        }

        #endregion

        #region 默认值

        /// <summary>
        /// 获取默认参数实例
        /// </summary>
        /// <typeparam name="TParams">参数类型</typeparam>
        /// <returns>默认参数实例</returns>
        public static TParams DefaultParams<TParams>() where TParams : ToolParameters, new()
        {
            return new TParams();
        }

        #endregion

        #region 异常处理

        /// <summary>
        /// 安全执行 - 捕获异常并返回结果
        /// </summary>
        /// <typeparam name="TResult">结果类型</typeparam>
        /// <param name="action">执行动作</param>
        /// <returns>执行结果</returns>
        public static TResult SafeRun<TResult>(Func<TResult> action)
            where TResult : ToolResults, new()
        {
            try
            {
                return action();
            }
            catch (Exception ex)
            {
                return Error<TResult>(ex.Message, ex);
            }
        }

        #endregion
    }
}
