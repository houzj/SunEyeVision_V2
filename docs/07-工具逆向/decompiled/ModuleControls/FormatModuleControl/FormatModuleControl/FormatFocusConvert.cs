using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FormatModuleControl;

public class FormatFocusConvert : IValueConverter
{
	object IValueConverter.Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is bool flag)
		{
			return flag ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F76900")) : new SolidColorBrush(Colors.Transparent);
		}
		return new SolidColorBrush(Colors.Transparent);
	}

	object IValueConverter.ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
