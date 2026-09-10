namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 显示文本数据 - DispText通道的轻量级文本载体
    /// </summary>
    public sealed class DisplayTextData
    {
        public string Content { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }

        /// <summary>
        /// 相对锚点的**屏幕像素**偏移 X（渲染层按 InvScale 反除，与字号同一缩放模型）。
        /// 用于"文本避开几何"的微偏移：缩放时与字号等比，不会出现间距被放大/缩小。
        /// </summary>
        public double ScreenOffsetX { get; set; }

        /// <summary>相对锚点的**屏幕像素**偏移 Y（同上）</summary>
        public double ScreenOffsetY { get; set; }
    }
}
