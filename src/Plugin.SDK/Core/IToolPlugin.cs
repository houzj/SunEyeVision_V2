using System;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具执行选项
    /// </summary>
    public sealed class ToolExecuteOptions
    {
        /// <summary>
        /// 是否启用详细日志
        /// </summary>
        public bool VerboseLogging { get; set; }

        /// <summary>
        /// 超时时间（毫秒），0表示无超时
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// 是否启用结果缓存
        /// </summary>
        public bool EnableCaching { get; set; } = true;

        /// <summary>
        /// 是否返回中间结果
        /// </summary>
        public bool ReturnIntermediateResults { get; set; }

        /// <summary>
        /// 用户自定义数据
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// 默认选项
        /// </summary>
        public static ToolExecuteOptions Default => new();
    }

    /// <summary>
    /// 工具插件接口 - 框架层使用的非泛型版本
    /// </summary>
    /// <remarks>
    /// 极简工具接口，插件开发唯一入口。
    /// 一个类就是一个完整的工具插件。
    /// 元数据通过 [Tool] 特性定义，框架通过反射自动获取。
    /// 
    /// 使用示例：
    /// <code>
    /// [Tool("threshold", "图像阈值化",
    ///     Description = "将灰度图像转换为二值图像",
    ///     Icon = "📷",
    ///     Category = "图像处理")]
    /// public class ThresholdTool : IToolPlugin&lt;ThresholdParameters, ThresholdResults&gt;
    /// {
    ///     public ThresholdResults Run(Mat image, ThresholdParameters parameters)
    ///     {
    ///         var result = new ThresholdResults();
    ///         Cv2.Threshold(image, result.OutputImage, parameters.Threshold, 255, ThresholdTypes.Binary);
    ///         return result;
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public interface IToolPlugin
    {
        /// <summary>参数类型</summary>
        Type ParamsType { get; }

        /// <summary>结果类型</summary>
        Type ResultType { get; }

        /// <summary>执行工具</summary>
        ToolResults Run(Mat image, ToolParameters parameters);

        /// <summary>是否支持调试窗口（默认 true）</summary>
        bool HasDebugWindow => true;

        /// <summary>创建调试窗口（可选实现）</summary>
        System.Windows.Window? CreateDebugWindow() => null;

        /// <summary>
        /// 创建调试控件（可选实现）
        /// </summary>
        /// <returns>调试控件实例，如果返回 null 则表示该工具无调试界面</returns>
        /// <remarks>
        /// 工具只负责创建内容，框架负责创建窗口。
        /// 框架根据 DebugWindowStyle 决定窗口类型，并将此控件放入窗口中。
        /// 
        /// 示例：
        /// <code>
        /// public FrameworkElement? CreateDebugControl()
        /// {
        ///     return new ThresholdToolDebugControl(this);
        /// }
        /// </code>
        /// </remarks>
        System.Windows.FrameworkElement? CreateDebugControl() => null;

        /// <summary>
        /// 创建数据分析控件（可选实现）
        /// </summary>
        /// <returns>数据分析控件实例，返回 null 表示该工具无数据分析界面</returns>
        /// <remarks>
        /// 用于主界面右下角"数据分析"Tab中展示工具执行结果的数据分析。
        /// 
        /// 与 CreateDebugControl() 的区别：
        /// - CreateDebugControl()   → 双击节点弹出的调试窗口内容（用于调参）
        /// - CreateDataAnalysisControl() → 右下角数据分析Tab内容（用于数据展示）
        /// 
        /// 框架行为：
        /// - 返回非null → 右下角显示"数据分析"Tab，Content 为此控件
        /// - 返回null  → 右下角隐藏"数据分析"Tab
        /// 
        /// 示例：
        /// <code>
        /// public FrameworkElement? CreateDataAnalysisControl()
        /// {
        ///     return new BlobDataAnalysisControl();
        /// }
        /// </code>
        /// </remarks>
        System.Windows.FrameworkElement? CreateDataAnalysisControl() => null;

        /// <summary>获取默认参数（可选实现）</summary>
        ToolParameters GetDefaultParameters() => (ToolParameters)Activator.CreateInstance(ParamsType)!;
    }

    /// <summary>
    /// 工具插件接口 - 开发者使用的泛型版本
    /// </summary>
    /// <typeparam name="TParams">参数类型</typeparam>
    /// <typeparam name="TResult">结果类型</typeparam>
    /// <remarks>
    /// 提供类型安全的执行入口，框架通过反射获取元数据。
    /// 
    /// 开发者只需：
    /// 1. 在类上标记 [Tool] 特性（定义元数据）
    /// 2. 实现 Run 方法（执行逻辑）
    /// 3. 可选重写 CreateDebugWindow 方法（调试窗口）
    /// </remarks>
    public interface IToolPlugin<TParams, TResult> : IToolPlugin
        where TParams : ToolParameters, new()
        where TResult : ToolResults, new()
    {
        /// <summary>执行工具（类型安全）</summary>
        TResult Run(Mat image, TParams parameters);

        /// <summary>获取默认参数</summary>
        new TParams GetDefaultParameters() => new();

        #region IToolPlugin 显式实现

        Type IToolPlugin.ParamsType => typeof(TParams);
        Type IToolPlugin.ResultType => typeof(TResult);

        ToolResults IToolPlugin.Run(Mat image, ToolParameters parameters)
        {
            if (parameters is TParams typedParams)
                return Run(image, typedParams);
            throw new ArgumentException($"参数类型错误：期望 {typeof(TParams).Name}，实际 {parameters?.GetType().Name ?? "null"}");
        }

        ToolParameters IToolPlugin.GetDefaultParameters() => new TParams();

        #endregion
    }

    /// <summary>
    /// 支持ROI的工具接口
    /// </summary>
    public interface IRoiToolPlugin<TParams, TResult> : IToolPlugin<TParams, TResult>
        where TParams : ToolParameters, new()
        where TResult : ToolResults, new()
    {
        /// <summary>执行工具（指定ROI）</summary>
        TResult Run(Mat image, TParams parameters, Models.Roi.IRoi roi);
    }
}
