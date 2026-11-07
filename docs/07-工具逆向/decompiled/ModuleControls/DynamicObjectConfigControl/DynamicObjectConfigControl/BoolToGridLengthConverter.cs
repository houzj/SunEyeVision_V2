using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DynamicObjectConfigControl;

public class BoolToGridLengthConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return new GridLength(1.0, GridUnitType.Star);
		}
		if (value is bool flag)
		{
			return flag ? new GridLength(1.0, GridUnitType.Star) : new GridLength(0.0);
		}
		return new GridLength(0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is GridLength gridLength)
		{
			return gridLength.Value > 0.0;
		}
		return false;
	}
}
