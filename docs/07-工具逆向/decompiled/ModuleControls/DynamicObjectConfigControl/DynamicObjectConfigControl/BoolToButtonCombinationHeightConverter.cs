using System;
using System.Globalization;
using System.Windows.Data;

namespace DynamicObjectConfigControl;

public class BoolToButtonCombinationHeightConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return 32;
		}
		if (value is bool flag)
		{
			return flag ? 32 : 0;
		}
		return 0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is int num)
		{
			return num != 0;
		}
		return false;
	}
}
