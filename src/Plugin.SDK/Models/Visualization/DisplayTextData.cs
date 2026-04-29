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
    }
}
