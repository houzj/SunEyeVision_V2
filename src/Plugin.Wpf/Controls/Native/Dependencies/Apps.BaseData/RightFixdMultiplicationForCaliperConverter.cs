using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

public class RightFixdMultiplicationForCaliperConverter : IMultiValueConverter
{
	private double fixedValue;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
		{
			return 0;
		}
		double num = System.Convert.ToDouble(values[0]);
		fixedValue = System.Convert.ToDouble(values[1]);
		if (num < 0.0)
		{
			return 5.0;
		}
		if (fixedValue == 0.0)
		{
			return double.NaN;
		}
		return num * fixedValue;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		double num = System.Convert.ToDouble(value);
		return new object[1] { num / fixedValue };
	}
}
