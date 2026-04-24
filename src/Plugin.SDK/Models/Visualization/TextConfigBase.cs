using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 文本配置基类 - SDK级文本配置
    /// </summary>
    /// <remarks>
    /// 提供文本显示的默认配置，支持OK/NG状态的颜色切换。
    /// UI层根据配置渲染文本。
    /// </remarks>
    public abstract class TextConfigBase
    {
        /// <summary>
        /// 分类名称
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 字体大小
        /// </summary>
        public double FontSize { get; set; } = 14.0;

        /// <summary>
        /// 是否加粗
        /// </summary>
        public bool IsBold { get; set; }

        /// <summary>
        /// OK状态颜色
        /// </summary>
        public Color OkColor { get; set; } = Colors.Lime;

        /// <summary>
        /// NG状态颜色
        /// </summary>
        public Color NgColor { get; set; } = Colors.Red;

        /// <summary>
        /// 获取指定状态的颜色
        /// </summary>
        public Color GetColor(bool isOk)
        {
            return isOk ? OkColor : NgColor;
        }

        /// <summary>
        /// 将颜色转换为ARGB格式
        /// </summary>
        public uint ToArgb(Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }
    }
}
