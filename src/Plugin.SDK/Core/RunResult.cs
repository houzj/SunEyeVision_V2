using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具运行结果 - 用于单工具调试场景
    /// </summary>
    /// <remarks>
    /// 封装工具执行结果，提供额外的调试信息。
    /// 
    /// 主要功能：
    /// 1. 包含工具执行结果
    /// 2. 记录执行时间和状态
    /// 3. 支持错误信息收集
    /// 4. 支持警告信息
    /// </remarks>
    public class RunResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus Status { get; set; }

        /// <summary>
        /// 工具结果
        /// </summary>
        public ToolResults? ToolResult { get; set; }

        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 错误堆栈
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 执行时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 工具名称
        /// </summary>
        public string? ToolName { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static RunResult Success(ToolResults toolResult, double executionTimeMs)
        {
            return new RunResult
            {
                IsSuccess = true,
                Status = ExecutionStatus.Success,
                ToolResult = toolResult,
                ExecutionTimeMs = executionTimeMs,
                ToolName = toolResult.ToolName
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static RunResult Failure(string errorMessage, Exception? exception = null)
        {
            return new RunResult
            {
                IsSuccess = false,
                Status = ExecutionStatus.Failed,
                ErrorMessage = errorMessage,
                ErrorStackTrace = exception?.StackTrace,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 创建取消结果
        /// </summary>
        public static RunResult Cancelled(string message = "操作已取消")
        {
            return new RunResult
            {
                IsSuccess = false,
                Status = ExecutionStatus.Cancelled,
                ErrorMessage = message,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 从 ToolResults 创建 RunResult
        /// </summary>
        public static RunResult FromToolResults(ToolResults toolResult)
        {
            return new RunResult
            {
                IsSuccess = toolResult.IsSuccess,
                Status = toolResult.Status,
                ToolResult = toolResult,
                ExecutionTimeMs = toolResult.ExecutionTimeMs,
                ErrorMessage = toolResult.ErrorMessage,
                ErrorStackTrace = toolResult.ErrorStackTrace,
                Warnings = toolResult.Warnings,
                Timestamp = toolResult.Timestamp,
                ToolName = toolResult.ToolName
            };
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }

    /// <summary>
    /// 泛型运行结果
    /// </summary>
    /// <typeparam name="TResult">工具结果类型</typeparam>
    public class RunResult<TResult> where TResult : ToolResults, new()
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// 执行状态
        /// </summary>
        public ExecutionStatus Status { get; set; }

        /// <summary>
        /// 工具结果
        /// </summary>
        public TResult? Result { get; set; }

        /// <summary>
        /// 执行时间（毫秒）
        /// </summary>
        public double ExecutionTimeMs { get; set; }

        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// 错误堆栈
        /// </summary>
        public string? ErrorStackTrace { get; set; }

        /// <summary>
        /// 警告信息列表
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// 执行时间戳
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 工具名称
        /// </summary>
        public string? ToolName { get; set; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static RunResult<TResult> Success(TResult result, double executionTimeMs)
        {
            return new RunResult<TResult>
            {
                IsSuccess = true,
                Status = ExecutionStatus.Success,
                Result = result,
                ExecutionTimeMs = executionTimeMs,
                ToolName = result.ToolName
            };
        }

        /// <summary>
        /// 创建失败结果
        /// </summary>
        public static RunResult<TResult> Failure(string errorMessage, Exception? exception = null)
        {
            return new RunResult<TResult>
            {
                IsSuccess = false,
                Status = ExecutionStatus.Failed,
                ErrorMessage = errorMessage,
                ErrorStackTrace = exception?.StackTrace,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 创建取消结果
        /// </summary>
        public static RunResult<TResult> Cancelled(string message = "操作已取消")
        {
            return new RunResult<TResult>
            {
                IsSuccess = false,
                Status = ExecutionStatus.Cancelled,
                ErrorMessage = message,
                Timestamp = DateTime.Now
            };
        }

        /// <summary>
        /// 从 ToolResults 转换
        /// </summary>
        public static RunResult<TResult> FromToolResults(TResult result)
        {
            return new RunResult<TResult>
            {
                IsSuccess = result.IsSuccess,
                Status = result.Status,
                Result = result,
                ExecutionTimeMs = result.ExecutionTimeMs,
                ErrorMessage = result.ErrorMessage,
                ErrorStackTrace = result.ErrorStackTrace,
                Warnings = result.Warnings,
                Timestamp = result.Timestamp,
                ToolName = result.ToolName
            };
        }

        /// <summary>
        /// 添加警告
        /// </summary>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }
    }
}
