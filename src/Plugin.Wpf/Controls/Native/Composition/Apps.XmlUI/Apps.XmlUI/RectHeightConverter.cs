using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class RectHeightConverter : IMultiValueConverter
{
	private double imageHeight = 0.0;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
		{
			return 0;
		}
		double num = System.Convert.ToDouble(values[0]);
		imageHeight = System.Convert.ToDouble(values[1]);
		if (imageHeight == 0.0)
		{
			return double.NaN;
		}
		int num2 = (int)(num * imageHeight);
		return (double)num2;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		double num = System.Convert.ToDouble(value);
		return new object[1] { num / imageHeight };
	}
}
