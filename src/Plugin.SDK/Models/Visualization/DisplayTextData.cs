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
        /// 相对锚点的基础屏幕像素偏移 X（以 zoom=1 为基准）。
        /// 渲染层与同一 Item 的文本内容一起应用幂律缩放矩阵。
        /// </summary>
        public double ScreenOffsetX { get; set; }

        /// <summary>相对锚点的基础屏幕像素偏移 Y（同上）</summary>
        public double ScreenOffsetY { get; set; }
    }
}
