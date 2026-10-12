using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Communication;

public sealed class ProtocolListViewportConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double available && double.IsFinite(available) ? Math.Max(0d, available - 32d) : 0d;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => DependencyProperty.UnsetValue;
}

public sealed class ProtocolMoveAvailableConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not int index || values[1] is not int count ||
            !int.TryParse(parameter?.ToString(), out var step)) return false;
        return index >= 0 && index < count && index + step >= 0 && index + step < count;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
