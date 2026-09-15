using System;
using System.Collections.Generic;
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
    /// 图像输入通过 ToolParameters.ResolvedInputImage 获取，
    /// 由参数绑定框架统一管理，不再作为 Run() 方法参数传递。
    /// 
    /// 使用示例：
    /// <code>
    /// [Tool("Threshold", "图像阈值化",
    ///     Description = "将灰度图像转换为二值图像",
    ///     Icon = "📷",
    ///     Category = "图像处理")]
    /// public class ThresholdTool : IToolPlugin&lt;ThresholdParameters, ThresholdResults&gt;
    /// {
    ///     public ThresholdResults Run(ThresholdParameters parameters)
    ///     {
    ///         var image = parameters.ResolvedInputImage
    ///             ?? throw new InvalidOperationException("未设置输入图像");
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

        /// <summary>
        /// 输出变量契约（SSOT）。来自工具定义/参数（设计时已知），
        /// 变量名 + 类型复用 SdkTypeVocabulary 固定词汇。
        /// 静态工具由框架基于 ResultType + GetPropertyMap 自动派生；
        /// 动态工具（脚本/子程序）由实现类从 Parameters 注入。
        /// </summary>
        IReadOnlyList<OutputVariable> OutputVariables { get; }

        /// <summary>执行工具</summary>
        /// <remarks>
        /// 图像输入通过 parameters.ResolvedInputImage 获取，
        /// 由参数绑定框架统一管理。
        /// </remarks>
        ToolResults Run(ToolParameters parameters);

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
    /// 3. 需要 WPF 编辑器时另行实现 IToolEditorFactory
    /// </remarks>
    public interface IToolPlugin<TParams, TResult> : IToolPlugin
        where TParams : ToolParameters, new()
        where TResult : ToolResults, new()
    {
        /// <summary>执行工具（类型安全）</summary>
        /// <remarks>
        /// 图像输入通过 parameters.ResolvedInputImage 获取，
        /// 由参数绑定框架统一管理。
        /// </remarks>
        TResult Run(TParams parameters);

        /// <summary>获取默认参数</summary>
        new TParams GetDefaultParameters() => new();

        #region IToolPlugin 显式实现

        Type IToolPlugin.ParamsType => typeof(TParams);
        Type IToolPlugin.ResultType => typeof(TResult);

        // 静态工具：框架基于 ResultType 的声明性 GetPropertyMap（不依赖运行值）自动派生契约 SSOT。
        IReadOnlyList<OutputVariable> IToolPlugin.OutputVariables => new TResult().GetOutputVariables();

        ToolResults IToolPlugin.Run(ToolParameters parameters)
        {
            if (parameters is TParams typedParams)
                return Run(typedParams);
            throw new ArgumentException($"参数类型错误：期望 {typeof(TParams).Name}，实际 {parameters?.GetType().Name ?? "null"}");
        }

        ToolParameters IToolPlugin.GetDefaultParameters() => new TParams();

        #endregion
    }

}
