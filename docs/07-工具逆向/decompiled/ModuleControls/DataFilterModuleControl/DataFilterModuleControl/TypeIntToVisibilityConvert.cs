using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DataFilterModuleControl;

public class TypeIntToVisibilityConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is string text)
		{
			if (text == "int")
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
