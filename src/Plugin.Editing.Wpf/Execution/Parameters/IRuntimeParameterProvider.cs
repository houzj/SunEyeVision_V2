using System;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters
{
    /// <summary>
    /// 运行时参数提供者接口
    /// </summary>
    /// <remarks>
    /// 定义运行时参数的存储和获取接口，用于支持外部系统在执行前注入参数值。
    /// 
    /// 典型应用场景：
    /// 1. 图像预览器点击图像时，将图像路径注入到执行上下文
    /// 2. 参数绑定为 RuntimeInjection 类型时，从上下文中获取对应的运行时参数
    /// 
    /// 使用示例：
    /// <code>
    /// // 在 MainWindow 中设置运行时参数
    /// _viewModel.SetRuntimeParameter(IRuntimeParameterProvider.CurrentImagePath, e.ImageInfo.FilePath);
    /// 
    /// // 在 ParameterResolver 中获取运行时参数
    /// var path = _runtimeParams.GetRuntimeParameter&lt;string&gt;(IRuntimeParameterProvider.CurrentImagePath);
    /// </code>
    /// </remarks>
    public interface IRuntimeParameterProvider
    {
        /// <summary>
        /// 获取运行时参数值
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数键名</param>
        /// <returns>参数值，如果不存在则返回默认值</returns>
        T? GetRuntimeParameter<T>(string key);

        /// <summary>
        /// 设置运行时参数值
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="key">参数键名</param>
        /// <param name="value">参数值</param>
        void SetRuntimeParameter<T>(string key, T value);

        /// <summary>
        /// 检查运行时参数是否存在
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <returns>是否存在</returns>
        bool HasRuntimeParameter(string key);

        /// <summary>
        /// 移除运行时参数
        /// </summary>
        /// <param name="key">参数键名</param>
        /// <returns>是否成功移除</returns>
        bool RemoveRuntimeParameter(string key);

        /// <summary>
        /// 清除所有运行时参数
        /// </summary>
        void ClearRuntimeParameters();

        #region 预定义键常量

        /// <summary>
        /// 预定义键：当前图像路径
        /// </summary>
        /// <remarks>
        /// 用于从图像预览器传递当前选中图像的文件路径。
        /// 通常由 ImageLoad 节点的 FilePath 参数绑定使用。
        /// </remarks>
        public const string CurrentImagePath = nameof(CurrentImagePath);

        /// <summary>
        /// 预定义键：当前图像索引
        /// </summary>
        /// <remarks>
        /// 用于传递当前图像在图像列表中的索引位置。
        /// </remarks>
        public const string CurrentImageIndex = nameof(CurrentImageIndex);

        /// <summary>
        /// 预定义键：当前图像对象
        /// </summary>
        /// <remarks>
        /// 用于传递当前图像对象本身（如 Mat、Bitmap 等）。
        /// </remarks>
        public const string CurrentImage = nameof(CurrentImage);

        /// <summary>
        /// 预定义键：工作流名称
        /// </summary>
        public const string WorkflowName = nameof(WorkflowName);

        /// <summary>
        /// 预定义键：执行ID
        /// </summary>
        public const string ExecutionId = nameof(ExecutionId);

        #endregion
    }
}
