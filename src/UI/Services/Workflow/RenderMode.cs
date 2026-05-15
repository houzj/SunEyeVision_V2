namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 窗口渲染模式
    /// </summary>
    public enum RenderMode
    {
        /// <summary>静态渲染：ToHObject + DispObj（默认行为）</summary>
        Static,

        /// <summary>交互渲染：HDrawingObject（可拖拽/缩放）</summary>
        Interactive
    }
}
