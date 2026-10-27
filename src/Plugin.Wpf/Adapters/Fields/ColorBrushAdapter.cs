using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>Converts only the WPF value representation; owns no palette or business defaults.</summary>
internal sealed class ColorBrushAdapter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var brush = new SolidColorBrush((Color)value);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is SolidColorBrush brush ? brush.Color : Binding.DoNothing;
}
