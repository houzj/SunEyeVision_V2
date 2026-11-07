using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace IMVSHPFeatureMatchModuControl;

public class DoubleToFloatConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return DependencyProperty.UnsetValue;
		}
		try
		{
			return ((float)double.Parse(value.ToString())).ToString();
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
			return double.Parse(value.ToString()).ToString();
		}
		catch
		{
			return DependencyProperty.UnsetValue;
		}
	}
}
