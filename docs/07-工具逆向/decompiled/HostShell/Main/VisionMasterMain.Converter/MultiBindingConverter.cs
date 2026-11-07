using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace VisionMasterMain.Converter;

public class MultiBindingConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		bool flag = false;
		if (values.Count() == 1)
		{
			if (values[0] is Visibility)
			{
				flag = (Visibility)values[0] == Visibility.Visible;
			}
		}
		else if (values.Count() == 2 && values[0] is Visibility && values[1] is Visibility)
		{
			flag = (Visibility)values[0] == Visibility.Visible || (Visibility)values[1] == Visibility.Visible;
		}
		return (!flag) ? Visibility.Collapsed : Visibility.Visible;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
