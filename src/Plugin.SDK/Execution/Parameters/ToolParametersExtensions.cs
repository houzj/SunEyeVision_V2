using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// ToolParameters 扩展方法 - 提供便捷的日志和上下文访问
    /// </summary>
    /// <remarks>
    /// 这些扩展方法让工具开发者可以方便地记录日志和访问执行上下文。
    /// 
    /// 使用示例：
    /// <code>
    /// public class MyTool : IToolPlugin&lt;MyParams, MyResult&gt;
    /// {
    ///     public MyResult Run(Mat image, MyParams p)
    ///     {
    ///         // 记录日志
    ///         p.LogInfo("开始处理");
    ///         p.LogDebug($"参数值: {p.SomeValue}");
    ///         
    ///         // 检查取消
    ///         if (p.IsCancellationRequested())
    ///             return MyResult.Cancelled();
    ///         
    ///         // 错误处理
    ///         try { ... }
    ///         catch (Exception ex) {
    ///             p.LogError("处理失败", ex);
    ///         }
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public static class ToolParametersExtensions
    {
        #region 日志访问

        /// <summary>
        /// 获取日志记录器
        /// </summary>
        /// <param name="parameters">参数对象</param>
        /// <returns>日志记录器，如果未配置则返回null</returns>
        public static ILogger? GetLogger(this ToolParameters parameters)
        {
            return parameters?.Context?.Logger;
        }

        /// <summary>
        /// 检查是否有日志支持
        /// </summary>
        public static bool HasLogger(this ToolParameters parameters)
        {
            return parameters?.Context?.Logger != null;
        }

        #endregion

        #region 日志记录方法

        /// <summary>
        /// 记录信息日志
        /// </summary>
        /// <param name="parameters">参数对象</param>
        /// <param name="message">日志消息</param>
        public static void LogInfo(this ToolParameters parameters, string message)
        {
            var context = parameters?.Context;
            context?.Logger?.Info(message, context.GetSourceName());
        }

        /// <summary>
        /// 记录成功日志
        /// </summary>
        /// <param name="parameters">参数对象</param>
        /// <param name="message">日志消息</param>
        public static void LogSuccess(this ToolParameters parameters, string message)
        {
            var context = parameters?.Context;
            context?.Logger?.Success(message, context.GetSourceName());
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        /// <param name="parameters">参数对象</param>
        /// <param name="message">日志消息</param>
        public static void LogWarning(this ToolParameters parameters, string message)
        {
            var context = parameters?.Context;
            context?.Logger?.Warning(message, context.GetSourceName());
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        /// <param name="parameters">参数对象</param>
        /// <param name="message">日志消息</param>
        /// <param name="exception">异常对象（可选）</param>
        public static void LogError(this ToolParameters parameters, string message, Exception? exception = null)
        {
            var context = parameters?.Context;
            context?.Logger?.Error(message, context.GetSourceName(), exception);
        }

        #endregion

        #region 取消令牌

        /// <summary>
        /// 检查是否请求取消
        /// </summary>
        public static bool IsCancellationRequested(this ToolParameters parameters)
        {
            return parameters?.Context?.IsCancellationRequested ?? false;
        }

        /// <summary>
        /// 获取取消令牌
        /// </summary>
        public static System.Threading.CancellationToken GetCancellationToken(this ToolParameters parameters)
        {
            return parameters?.Context?.CancellationToken ?? System.Threading.CancellationToken.None;
        }

        /// <summary>
        /// 如果请求取消则抛出异常
        /// </summary>
        public static void ThrowIfCancellationRequested(this ToolParameters parameters)
        {
            parameters?.Context?.ThrowIfCancellationRequested();
        }

        #endregion

        #region 上下文信息

        /// <summary>
        /// 获取工具名称
        /// </summary>
        public static string? GetToolName(this ToolParameters parameters)
        {
            return parameters?.Context?.ToolName;
        }

        /// <summary>
        /// 获取节点名称
        /// </summary>
        public static string? GetNodeName(this ToolParameters parameters)
        {
            return parameters?.Context?.NodeName;
        }

        /// <summary>
        /// 获取节点ID
        /// </summary>
        public static string? GetNodeId(this ToolParameters parameters)
        {
            return parameters?.Context?.NodeId;
        }

        /// <summary>
        /// 获取日志来源名称
        /// </summary>
        public static string GetSourceName(this ToolParameters parameters)
        {
            return parameters?.Context?.GetSourceName() ?? "Tool";
        }

        #endregion
    }
}
