using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace FewShotCommonControl;

public class BoolToCollapsedConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is bool flag)
		{
			if (flag)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
