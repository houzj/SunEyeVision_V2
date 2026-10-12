using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Communication;

public sealed class CommunicationOrdinalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is int index ? index + 1 : 1;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
