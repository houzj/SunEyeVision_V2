using System;
using System.Collections.Generic;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Workflow
{
    /// <summary>
    /// 算法执行结果
    /// </summary>
    public class AlgorithmResult
    {
        /// <summary>
        /// 算法名称
        /// </summary>
        public string AlgorithmName { get; set; } = string.Empty;

        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// 结果图像
        /// </summary>
        public HImage? ResultImage { get; set; }

        /// <summary>
        /// 错误消息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 执行时间
        /// </summary>
        public DateTime ExecutionTime { get; set; }

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public double ExecutionDurationMs { get; set; }

        /// <summary>
        /// 原始工具结果引用（用于保留完整结果信息）
        /// </summary>
        public ToolResults? ToolResults { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static AlgorithmResult CreateSuccess(HImage? resultImage, double durationMs, string algorithmName = "")
        {
            return new AlgorithmResult
            {
                AlgorithmName = algorithmName,
                Success = true,
                ResultImage = resultImage,
                ExecutionTime = DateTime.Now,
                ExecutionDurationMs = durationMs
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static AlgorithmResult CreateFailure(string errorMessage, string algorithmName = "")
        {
            return new AlgorithmResult
            {
                AlgorithmName = algorithmName,
                Success = false,
                ErrorMessage = errorMessage,
                ExecutionTime = DateTime.Now
            };
        }

        /// <summary>
        /// 创建错误结果（CreateFailure的别名）
        /// </summary>
        public static AlgorithmResult CreateError(string errorMessage, string algorithmName = "")
        {
            return CreateFailure(errorMessage, algorithmName);
        }
    }
}
