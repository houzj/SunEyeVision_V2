using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class CircleSpaceConverter : IMultiValueConverter
{
	private double radius = 0.0;

	private double minRadius = 0.0;

	private double imageWidth = 0.0;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue || values[2] == DependencyProperty.UnsetValue)
		{
			return 0;
		}
		radius = System.Convert.ToDouble(values[0]);
		minRadius = System.Convert.ToDouble(values[1]);
		imageWidth = System.Convert.ToDouble(values[2]);
		if (imageWidth == 0.0)
		{
			return double.NaN;
		}
		int num = System.Convert.ToInt32(radius * imageWidth - minRadius * imageWidth);
		return (double)num;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		double num = System.Convert.ToDouble(value);
		int num2 = (int)(radius * imageWidth - minRadius * imageWidth);
		if (num2 == (int)num)
		{
			return new object[2] { radius, minRadius };
		}
		return new object[2]
		{
			num / imageWidth + minRadius,
			minRadius
		};
	}
}
