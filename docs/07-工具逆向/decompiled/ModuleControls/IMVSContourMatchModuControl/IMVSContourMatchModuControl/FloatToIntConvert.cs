using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IMVSContourMatchModuControl;

public class FloatToIntConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		try
		{
			return ((int)float.Parse(value.ToString())).ToString();
		}
		catch
		{
			return DependencyProperty.UnsetValue;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		try
		{
			return float.Parse(value.ToString()).ToString();
		}
		catch
		{
			return DependencyProperty.UnsetValue;
		}
	}
}
