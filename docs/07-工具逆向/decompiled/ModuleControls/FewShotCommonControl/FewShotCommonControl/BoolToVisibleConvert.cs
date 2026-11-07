using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FewShotCommonControl;

public class BoolToVisibleConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is bool flag)
		{
			if (flag)
			{
				return Visibility.Visible;
			}
			return Visibility.Collapsed;
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
