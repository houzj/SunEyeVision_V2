using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class RadiusConverter : IMultiValueConverter
{
	private double imageWidth = 0.0;

	private double radiusRate = 0.0;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || values[1] == DependencyProperty.UnsetValue)
		{
			return 0;
		}
		radiusRate = System.Convert.ToDouble(values[0]);
		imageWidth = System.Convert.ToDouble(values[1]);
		if (imageWidth == 0.0)
		{
			return double.NaN;
		}
		int num = System.Convert.ToInt32(radiusRate * imageWidth * 2.0);
		return (double)num;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		double num = System.Convert.ToDouble(value);
		int num2 = (int)(radiusRate * imageWidth * 2.0);
		if (num2 == (int)num)
		{
			return new object[1] { radiusRate };
		}
		return new object[1] { num / (imageWidth * 2.0) };
	}
}
