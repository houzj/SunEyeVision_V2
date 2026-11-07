namespace SunEyeVision.Plugin.Editing.Wpf.Core
{
    /// <summary>
    /// 调试窗口样式枚举
    /// </summary>
    /// <remarks>
    /// 定义节点双击时的窗口打开行为：
    /// - None: 无窗口（不打开任何窗口）
    /// - Default: 标准窗口（有标题栏和边框）
    /// - Custom: 自定义窗口（无边框圆角窗口）
    /// 
    /// 窗口类型优先级：节点级配置 → 工具级配置 → 全局默认值
    /// </remarks>
    public enum DebugWindowStyle
    {
        /// <summary>
        /// 无窗口 - 不打开任何窗口
        /// </summary>
        /// <remarks>
        /// 适用于不需要调试界面的工具（如纯数据流节点）。
        /// 双击此类节点时，不会打开任何窗口。
        /// </remarks>
        None,

        /// <summary>
        /// 标准窗口 - 有标题栏和边框
        /// </summary>
        /// <remarks>
        /// 适用于大多数工具的默认窗口样式。
        /// 标准的 WPF 窗口，包含标题栏、边框、最小化/最大化/关闭按钮。
        /// </remarks>
        Default,

        /// <summary>
        /// 自定义窗口 - 无边框圆角窗口
        /// </summary>
        /// <remarks>
        /// 适用于需要特殊视觉效果的工具。
        /// 无边框窗口，带有圆角和自定义样式。
        /// </remarks>
        Custom
    }
}
