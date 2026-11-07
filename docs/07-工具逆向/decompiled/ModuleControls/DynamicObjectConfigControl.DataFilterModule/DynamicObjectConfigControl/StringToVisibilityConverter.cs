using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace DynamicObjectConfigControl;

public class StringToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		if (!string.IsNullOrEmpty(text) && text.Equals("DynamicOutputParam"))
		{
			return Visibility.Visible;
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
