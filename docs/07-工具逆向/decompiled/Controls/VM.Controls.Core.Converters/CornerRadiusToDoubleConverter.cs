using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VM.Controls.Core.Converters;

public class CornerRadiusToDoubleConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double num = 0.0;
		if (value != null)
		{
			num = ((CornerRadius)value).TopLeft;
		}
		return num;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double uniformRadius = 0.0;
		if (value != null)
		{
			uniformRadius = (double)value;
		}
		return new CornerRadius(uniformRadius);
	}
}
