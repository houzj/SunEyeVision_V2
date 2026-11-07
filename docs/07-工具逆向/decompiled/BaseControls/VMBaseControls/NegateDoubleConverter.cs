using System;
using System.Globalization;
using System.Windows.Data;

namespace VMBaseControls;

public class NegateDoubleConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (value is double) ? ((object)(0.0 - (double)value)) : value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Convert(value, targetType, parameter, culture);
	}
}
