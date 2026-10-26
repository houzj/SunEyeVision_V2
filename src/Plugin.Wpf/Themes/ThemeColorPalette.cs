using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Themes
{
    /// <summary>
    /// 主题颜色系列，包含基础色和变体
    /// </summary>
    public class ThemeColorSeries
    {
        /// <summary>
        /// 颜色系列名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 基础颜色
        /// </summary>
        public Color BaseColor { get; }

        /// <summary>
        /// 浅色变体（从浅到深）
        /// </summary>
        public List<Color> LighterVariants { get; }

        /// <summary>
        /// 深色变体（从深到浅）
        /// </summary>
        public List<Color> DarkerVariants { get; }

        /// <summary>
        /// 所有颜色（浅色变体 + 基础色 + 深色变体）
        /// </summary>
        public List<Color> AllColors { get; }

        public ThemeColorSeries(string name, Color baseColor)
        {
            Name = name;
            BaseColor = baseColor;
            LighterVariants = new List<Color>();
            DarkerVariants = new List<Color>();
            AllColors = new List<Color>();

            // 生成浅色变体（3个，越来越浅）
            for (int i = 3; i >= 1; i--)
            {
                var lighter = LightenColor(baseColor, i * 0.12);
                LighterVariants.Add(lighter);
                AllColors.Add(lighter);
            }

            // 添加基础色
            AllColors.Add(baseColor);

            // 生成深色变体（2个，越来越深）
            for (int i = 1; i <= 2; i++)
            {
                var darker = DarkenColor(baseColor, i * 0.12);
                DarkerVariants.Add(darker);
                AllColors.Add(darker);
            }
        }

        /// <summary>
        /// 使颜色变浅
        /// </summary>
        private static Color LightenColor(Color color, double amount)
        {
            byte r = (byte)Math.Min(255, color.R + (255 - color.R) * amount);
            byte g = (byte)Math.Min(255, color.G + (255 - color.G) * amount);
            byte b = (byte)Math.Min(255, color.B + (255 - color.B) * amount);
            return Color.FromArgb(255, r, g, b);
        }

        /// <summary>
        /// 使颜色变深
        /// </summary>
        private static Color DarkenColor(Color color, double amount)
        {
            byte r = (byte)Math.Max(0, color.R * (1 - amount));
            byte g = (byte)Math.Max(0, color.G * (1 - amount));
            byte b = (byte)Math.Max(0, color.B * (1 - amount));
            return Color.FromArgb(255, r, g, b);
        }
    }

    /// <summary>
    /// 主题颜色调色板，集中管理所有主题颜色和标准颜色
    /// </summary>
    public static class ThemeColorPalette
    {
        /// <summary>
        /// 主题颜色系列列表（10个系列，每系列5个变体）
        /// </summary>
        public static List<ThemeColorSeries> ThemeColorSeriesList { get; }

        /// <summary>
        /// 标准颜色列表（10个常用颜色，无变体）
        /// </summary>
        public static List<Color> StandardColors { get; }

        static ThemeColorPalette()
        {
            // 初始化主题颜色系列（10个系列）
            ThemeColorSeriesList = new List<ThemeColorSeries>
            {
                new ThemeColorSeries("深红", Color.FromRgb(0xC0, 0x00, 0x00)),
                new ThemeColorSeries("红色", Color.FromRgb(0xFF, 0x00, 0x00)),
                new ThemeColorSeries("橙色", Color.FromRgb(0xFF, 0xC0, 0x00)),
                new ThemeColorSeries("黄色", Color.FromRgb(0xFF, 0xFF, 0x00)),
                new ThemeColorSeries("浅绿", Color.FromRgb(0x92, 0xD0, 0x50)),
                new ThemeColorSeries("绿色", Color.FromRgb(0x00, 0xB0, 0x50)),
                new ThemeColorSeries("浅蓝", Color.FromRgb(0x00, 0xB0, 0xF0)),
                new ThemeColorSeries("蓝色", Color.FromRgb(0x00, 0x70, 0xC0)),
                new ThemeColorSeries("紫色", Color.FromRgb(0x70, 0x30, 0xA0)),
                new ThemeColorSeries("品红", Color.FromRgb(0xFF, 0x00, 0xFF)),
            };

            // 初始化标准颜色（黑白三原色及常用颜色）
            StandardColors = new List<Color>
            {
                Color.FromRgb(0, 0, 0),       // 黑色
                Color.FromRgb(255, 255, 255), // 白色
                Color.FromRgb(255, 0, 0),     // 红色（三原色）
                Color.FromRgb(0, 255, 0),     // 绿色（三原色）
                Color.FromRgb(0, 0, 255),     // 蓝色（三原色）
                Color.FromRgb(255, 255, 0),   // 黄色
                Color.FromRgb(255, 0, 255),   // 品红
                Color.FromRgb(0, 255, 255),   // 青色
                Color.FromRgb(128, 128, 128), // 灰色
                Color.FromRgb(255, 128, 0),   // 橙色
            };
        }

        /// <summary>
        /// 将 Color 转换为 uint ARGB 格式
        /// </summary>
        public static uint ColorToUInt(Color color)
        {
            return (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | color.B);
        }

        /// <summary>
        /// 将 uint ARGB 格式转换为 Color
        /// </summary>
        public static Color UIntToColor(uint argb)
        {
            byte a = (byte)((argb >> 24) & 0xFF);
            byte r = (byte)((argb >> 16) & 0xFF);
            byte g = (byte)((argb >> 8) & 0xFF);
            byte b = (byte)(argb & 0xFF);
            return Color.FromArgb(a, r, g, b);
        }
    }
}
