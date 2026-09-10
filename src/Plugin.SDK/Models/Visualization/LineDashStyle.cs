namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 线条线型（虚线样式）。
    /// 默认 <see cref="Solid"/>：既有工具样式不显式设置时行为完全不变（零影响）。
    /// 渲染层按节距生成 DashStyle，节距单位为线宽倍数（缩放时线宽等比变化，虚线观感稳定）。
    /// </summary>
    public enum LineDashStyle
    {
        /// <summary>实线（默认）</summary>
        Solid,

        /// <summary>虚线（长划）</summary>
        Dash,

        /// <summary>点线（短划）</summary>
        Dot,

        /// <summary>点划线（长划 + 点）</summary>
        DashDot,
    }
}
