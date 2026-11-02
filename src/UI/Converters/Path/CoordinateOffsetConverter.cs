using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.UI.Converters.Path
{
    /// <summary>Offsets a canvas coordinate so a path marker is centered on its point.</summary>
    public sealed class CoordinateOffsetConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double coordinate || !double.IsFinite(coordinate) ||
                parameter is not string text ||
                !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var offset) ||
                !double.IsFinite(offset) || !double.IsFinite(coordinate + offset))
                return DependencyProperty.UnsetValue;
            return coordinate + offset;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}