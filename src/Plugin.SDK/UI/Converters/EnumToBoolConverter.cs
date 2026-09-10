using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.SDK.UI.Converters
{
    /// <summary>
    /// 枚举 ↔ 布尔转换器（RadioButton 双向绑定用）。
    /// </summary>
    /// <remarks>
    /// 用法：<c>IsChecked="{Binding Mode, Converter={StaticResource EnumToBoolConverter}, ConverterParameter=ByShape}"</c>
    /// <para>
    /// 与 <c>RoiEditor.Converters.ModeToBoolConverter</c> 的区别：本转换器不硬编码枚举类型，
    /// 由 <c>ConverterParameter</c> 指定枚举成员名、由绑定源类型决定枚举类型，可跨模块复用。
    /// </para>
    /// </remarks>
    public class EnumToBoolConverter : IValueConverter
    {
        /// <summary>值等于参数指定的枚举成员时返回 true。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter is not string targetMember || string.IsNullOrEmpty(targetMember))
                return false;

            return string.Equals(value.ToString(), targetMember, StringComparison.Ordinal);
        }

        /// <summary>选中（true）时把参数指定的枚举成员写回绑定源；取消选中不产生写入。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isChecked && isChecked && parameter is string targetMember)
            {
                var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
                if (enumType.IsEnum && Enum.TryParse(enumType, targetMember, out var parsed))
                    return parsed;
            }

            return DependencyProperty.UnsetValue;
        }
    }
}
