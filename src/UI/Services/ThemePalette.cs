using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 语义色板门面：供代码层（Converters/渲染）从资源字典现取现用语义颜色/笔刷。
    /// 每次调用通过 FindResource 实时查找（不缓存），确保主题切换后取到新值。
    /// 未命中的 Key 直接抛 KeyNotFoundException（rule-015 禁止兜底回退）。
    /// </summary>
    public static class ThemePalette
    {
        private static readonly VisionLogger Logger = VisionLogger.Instance;

        /// <summary>
        /// 获取语义 Key 对应的笔刷。
        /// </summary>
        /// <param name="key">语义 Key（ThemeKeys 常量）</param>
        /// <returns>对应笔刷</returns>
        /// <exception cref="KeyNotFoundException">当前主题未定义该 Key 时抛出</exception>
        public static SolidColorBrush GetBrush(string key)
        {
            if (Application.Current?.TryFindResource(key) is SolidColorBrush brush)
            {
                return brush;
            }

            Logger.Log(LogLevel.Error, $"主题语义笔刷未定义: {key}", "ThemePalette");
            throw new KeyNotFoundException($"当前主题未定义语义笔刷 Key: {key}");
        }

        /// <summary>
        /// 获取语义 Key 对应的颜色。
        /// </summary>
        /// <param name="key">语义 Key（ThemeKeys 常量）</param>
        /// <returns>对应颜色</returns>
        /// <exception cref="KeyNotFoundException">当前主题未定义该 Key 时抛出</exception>
        public static Color GetColor(string key)
        {
            if (Application.Current?.TryFindResource(key) is Color color)
            {
                return color;
            }

            Logger.Log(LogLevel.Error, $"主题语义颜色未定义: {key}", "ThemePalette");
            throw new KeyNotFoundException($"当前主题未定义语义颜色 Key: {key}");
        }
    }
}
