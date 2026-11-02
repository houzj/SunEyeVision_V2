using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.UI.Features.Camera;

public sealed class CameraNumericFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is int digits ? "F" + Math.Clamp(digits, 0, 15).ToString(CultureInfo.InvariantCulture) : "G";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
