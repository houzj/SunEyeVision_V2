using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

public class NormalMultiplicationConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == DependencyProperty.UnsetValue)
		{
			return double.NaN;
		}
		double num = System.Convert.ToDouble(value);
		double num2 = System.Convert.ToDouble(parameter);
		return num * num2;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
