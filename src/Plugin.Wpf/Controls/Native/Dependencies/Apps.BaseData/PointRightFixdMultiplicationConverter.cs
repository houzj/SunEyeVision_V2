using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

public class PointRightFixdMultiplicationConverter : IMultiValueConverter
{
	private double fixedWidth;

	private double fixedHeight;

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue || !(values[0] is Point) || values[1] == DependencyProperty.UnsetValue || values[2] == DependencyProperty.UnsetValue)
		{
			return 0;
		}
		Point point = (Point)values[0];
		fixedWidth = System.Convert.ToDouble(values[1]);
		if (fixedWidth == 0.0)
		{
			return new Point(double.NaN, double.NaN);
		}
		fixedHeight = System.Convert.ToDouble(values[2]);
		if (fixedHeight == 0.0)
		{
			return new Point(double.NaN, double.NaN);
		}
		return new Point(point.X * fixedWidth, point.Y * fixedHeight);
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		Point point = (Point)value;
		return new object[1]
		{
			new Point(point.X / fixedWidth, point.Y / fixedHeight)
		};
	}
}
