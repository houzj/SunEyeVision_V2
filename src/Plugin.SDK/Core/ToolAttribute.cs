using System;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具特性 - 工具元数据的单一数据源（Single Source of Truth）
    /// </summary>
    /// <remarks>
    /// 此特性标记在工具类上，定义工具的所有元数据。
    /// ITool 接口属性、ToolMetadata、IToolPlugin 接口属性都从此特性自动派生。
    /// 
    /// 使用示例：
    /// <code>
    /// [Tool(
    ///     id: "Threshold", 
    ///     displayName: "图像阈值化",
    ///     Description = "将灰度图像转换为二值图像",
    ///     Icon = "📷",
    ///     Category = "图像处理",
    ///     Version = "2.0.0"
    /// )]
    /// public class ThresholdTool : ToolBase&lt;ThresholdParameters, ThresholdResults&gt;
    /// {
    ///     public override ThresholdResults Run(Mat image, ThresholdParameters parameters)
    ///     {
    ///         // 执行逻辑
    ///     }
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ToolAttribute : Attribute
    {
        /// <summary>
        /// 工具ID（唯一标识符）
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// 显示名称（UI显示）
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 工具描述
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 工具语义矢量图标键（由SDK ToolIcon控件统一呈现）
        /// </summary>
        public string Icon { get; set; } = "generic";

        /// <summary>分类内显式顺序；未声明的扩展工具排在已编排工具之后。</summary>
        public int Order { get; set; } = 1000;

        /// <summary>
        /// 工具分类
        /// </summary>
        public string Category { get; set; } = "未分类";

        /// <summary>
        /// 工具版本
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// 工具作者
        /// </summary>
        public string Author { get; set; } = "SunEyeVision";

        /// <summary>
        /// 是否支持调试窗口
        /// </summary>
        /// <remarks>
        /// 无窗口工具（如 ImageLoadTool）应设置为 false。
        /// </remarks>
        public bool HasDebugWindow { get; set; } = true;

        /// <summary>
        /// 调试窗口样式
        /// </summary>
        /// <remarks>
        /// 定义节点双击时的窗口打开行为：
        /// - None: 无窗口（不打开任何窗口）
        /// - Default: 标准窗口（有标题栏和边框）
        /// - Custom: 自定义窗口（无边框圆角窗口）
        /// 
        /// 默认值为 Default（标准窗口）。
        /// </remarks>
        public DebugWindowStyle DebugWindowStyle { get; set; } = DebugWindowStyle.Default;

        /// <summary>
        /// 创建工具特性
        /// </summary>
        /// <param name="id">工具ID（唯一标识符）</param>
        /// <param name="displayName">显示名称（UI显示）</param>
        public ToolAttribute(string id, string displayName)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        }
    }
}
