using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 逐字迁移自原厂 PyramidModeToEnableCvt.cs（IMVSContourMatchModuControl）。
/// SelectedIndex 0（手动）→ enabled=true；1（自动）→ false。
/// </summary>
public sealed class PyramidModeToEnableCvt : IValueConverter
{
    public static readonly PyramidModeToEnableCvt Cvt = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => int.TryParse(value?.ToString(), out var result) && result == 0;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
