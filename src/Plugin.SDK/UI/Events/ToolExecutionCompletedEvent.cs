using System;
using System.Windows;

namespace SunEyeVision.Plugin.SDK.UI.Events
{
    /// <summary>
    /// 工具执行完成事件参数
    /// </summary>
    /// <remarks>
    /// 使用说明：
    /// - 包含执行结果和状态信息
    /// - 支持 WPF 路由事件机制
    /// - 可在 XAML 中声明式绑定
    /// 
    /// XAML 使用示例：
    /// <![CDATA[
    /// <UserControl x:Class="MyToolDebugControl"
    ///              xmlns:events="clr-namespace:SunEyeVision.Plugin.SDK.UI.Events;assembly=SunEyeVision.Plugin.SDK">
    ///     
    ///     <!-- 声明式绑定路由事件 -->
    ///     <UserControl.CommandBindings>
    ///         <CommandBinding Command="commands:ToolCommands.Execute"
    ///                        Executed="OnExecuteCommand"/>
    ///     </UserControl.CommandBindings>
    ///     
    ///     <!-- 声明式绑定路由事件 -->
    ///     <UserControl>
    ///         <i:Interaction.Triggers>
    ///             <i:EventTrigger EventName="ToolExecutionCompleted">
    ///                 <!-- 事件处理 -->
    ///             </i:EventTrigger>
    ///         </i:Interaction.Triggers>
    ///     </UserControl>
    /// </UserControl>
    /// ]]>
    /// </remarks>
    public class ToolExecutionCompletedEventArgs : RoutedEventArgs
    {
        #region 属性

        /// <summary>
        /// 执行结果对象（强类型）
        /// </summary>
        public object? Results { get; }

        /// <summary>
        /// 执行是否成功
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 执行耗时（毫秒）
        /// </summary>
        public long ExecutionTime { get; }

        /// <summary>
        /// 错误信息（如果失败）
        /// </summary>
        public string? ErrorMessage { get; }

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建工具执行完成事件参数
        /// </summary>
        /// <param name="results">执行结果</param>
        /// <param name="routedEvent">路由事件</param>
        /// <param name="source">事件源</param>
        public ToolExecutionCompletedEventArgs(
            object? results,
            RoutedEvent routedEvent,
            object source)
            : base(routedEvent, source)
        {
            Results = results;
            IsSuccess = results is SunEyeVision.Plugin.SDK.Execution.Results.ToolResults toolResult?toolResult.IsSuccess:results is not null;
            ErrorMessage=IsSuccess?null:(results as SunEyeVision.Plugin.SDK.Execution.Results.ToolResults)?.ErrorMessage??"执行未返回有效结果。";
        }

        /// <summary>
        /// 创建工具执行完成事件参数（带执行时间）
        /// </summary>
        /// <param name="results">执行结果</param>
        /// <param name="executionTime">执行耗时（毫秒）</param>
        /// <param name="routedEvent">路由事件</param>
        /// <param name="source">事件源</param>
        public ToolExecutionCompletedEventArgs(
            object? results,
            long executionTime,
            RoutedEvent routedEvent,
            object source)
            : base(routedEvent, source)
        {
            Results = results;
            IsSuccess = results is SunEyeVision.Plugin.SDK.Execution.Results.ToolResults toolResult?toolResult.IsSuccess:results is not null;
            ErrorMessage=IsSuccess?null:(results as SunEyeVision.Plugin.SDK.Execution.Results.ToolResults)?.ErrorMessage??"执行未返回有效结果。";
            ExecutionTime = executionTime;
        }

        /// <summary>
        /// 创建工具执行失败事件参数
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        /// <param name="routedEvent">路由事件</param>
        /// <param name="source">事件源</param>
        public ToolExecutionCompletedEventArgs(
            string errorMessage,
            RoutedEvent routedEvent,
            object source)
            : base(routedEvent, source)
        {
            IsSuccess = false;
            ErrorMessage = errorMessage;
        }

        #endregion
    }

    /// <summary>
    /// 工具执行完成事件处理委托
    /// </summary>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    public delegate void ToolExecutionCompletedEventHandler(object sender, ToolExecutionCompletedEventArgs e);

    /// <summary>
    /// 工具执行完成事件参数（泛型版本）
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    public class ToolExecutionCompletedEventArgs<T> : ToolExecutionCompletedEventArgs
    {
        /// <summary>
        /// 强类型执行结果
        /// </summary>
        public T? TypedResults => (T?)Results;

        /// <summary>
        /// 创建工具执行完成事件参数
        /// </summary>
        public ToolExecutionCompletedEventArgs(T results, RoutedEvent routedEvent)
            : base(results, routedEvent, null!)
        {
        }

        /// <summary>
        /// 创建工具执行完成事件参数（带执行时间）
        /// </summary>
        public ToolExecutionCompletedEventArgs(T results, long executionTime, RoutedEvent routedEvent)
            : base(results, executionTime, routedEvent, null!)
        {
        }
    }

    /// <summary>
    /// 工具执行完成事件处理委托（泛型版本）
    /// </summary>
    /// <typeparam name="T">结果类型</typeparam>
    /// <param name="sender">事件发送者</param>
    /// <param name="e">事件参数</param>
    public delegate void ToolExecutionCompletedEventHandler<T>(object sender, ToolExecutionCompletedEventArgs<T> e);
}
