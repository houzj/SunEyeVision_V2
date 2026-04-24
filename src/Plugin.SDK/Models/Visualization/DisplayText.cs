using System;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 显示文本数据模型 - DispText通道的轻量级文本数据
    /// </summary>
    /// <remarks>
    /// 独立于SDK的几何类型，用于描述需要显示的文本标签。
    /// UI层负责将文本渲染到 DrawingContext。
    /// </remarks>
    public sealed class DisplayText
    {
        /// <summary>
        /// 文本内容
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// X坐标（图像坐标系）
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Y坐标（图像坐标系）
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// 颜色（ARGB格式）
        /// </summary>
        public uint Color { get; set; } = 0xFFFFFFFF;

        /// <summary>
        /// 字体大小
        /// </summary>
        public double FontSize { get; set; } = 14.0;

        /// <summary>
        /// 是否加粗
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// 分类名称（用于分类控制可见性）
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 是否OK状态（影响颜色）
        /// </summary>
        public bool IsOk { get; set; } = true;

        /// <summary>
        /// Z顺序
        /// </summary>
        public int ZOrder { get; set; }

        /// <summary>
        /// 创建文本
        /// </summary>
        public static DisplayText Create(string content, double x, double y, uint color = 0xFFFFFFFF, double fontSize = 14.0)
        {
            return new DisplayText
            {
                Content = content,
                X = x,
                Y = y,
                Color = color,
                FontSize = fontSize
            };
        }
    }
}
