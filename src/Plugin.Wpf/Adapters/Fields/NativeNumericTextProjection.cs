using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>One fixed-precision projection shared by the original float binding and its transaction owner.</summary>
internal static class NativeNumericTextProjection
{
    internal static string OriginalFormat(int digits) => digits is >= 0 and < 10 ? "0." + new string('0', digits) : "0.000";
    internal static bool TryParseFinite(string text, out double value)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    internal static string Format(double value, int digits)
    {
        if (!double.IsFinite(value)) throw new ArgumentException("请输入有限数值。");
        var precision = digits is >= 0 and < 10 ? digits : 3;
        var normalized = Math.Round(value, precision, MidpointRounding.ToEven);
        var original = normalized.ToString(OriginalFormat(digits), CultureInfo.InvariantCulture);
        if (TryParseFinite(original, out var parsed) && parsed == normalized) return original;
        // Fixed-point fallback remains directly editable and cannot introduce exponent-only text.
        var fallback = normalized.ToString("F" + precision, CultureInfo.InvariantCulture);
        if (TryParseFinite(fallback, out parsed) && parsed == normalized) return fallback;
        throw new ArgumentException("当前数值不能按声明精度有限往返。");
    }
}

internal sealed class NativeNumericTextConverter(Func<int> digits) : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is double number && double.IsFinite(number) ? NativeNumericTextProjection.Format(number, digits()) : DependencyProperty.UnsetValue;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is string text && NativeNumericTextProjection.TryParseFinite(text, out var number) ? number : DependencyProperty.UnsetValue;
}
