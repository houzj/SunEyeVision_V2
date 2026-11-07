using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FormatModuleControl;

public class BoolToVisibilityDeleteButtonConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is bool)
		{
			if ((bool)value)
			{
				return Visibility.Visible;
			}
			return Visibility.Hidden;
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
