using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DataFilterModuleControl;

public class TypeFloatToVisibilityConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string text)
		{
			if (text == "float")
			{
				return Visibility.Visible;
			}
			return Visibility.Hidden;
		}
		return Visibility.Hidden;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
