using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.SDK.UI.Converters
{
    /// <summary>
    /// 枚举 → 可见性转换器（按枚举成员切换面板显隐）。
    /// </summary>
    /// <remarks>
    /// 用法：<c>Visibility="{Binding Mode, Converter={StaticResource EnumToVisibilityConverter}, ConverterParameter=ByShape}"</c>
    /// <para>
    /// <c>ConverterParameter</c> 支持多个成员名（逗号分隔，任一命中即 Visible），
    /// 便于"多个枚举值共用同一面板"的场景。
    /// </para>
    /// </remarks>
    public class EnumToVisibilityConverter : IValueConverter
    {
        /// <summary>值属于参数指定的枚举成员集合时返回 Visible，否则 Collapsed。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter is not string members || string.IsNullOrEmpty(members))
                return Visibility.Collapsed;

            var current = value.ToString();
            foreach (var member in members.Split(','))
            {
                if (string.Equals(current, member.Trim(), StringComparison.Ordinal))
                    return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        /// <summary>单向转换：可见性不反写枚举。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("EnumToVisibilityConverter 仅支持单向转换。");
    }
}
