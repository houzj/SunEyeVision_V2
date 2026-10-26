using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public class PointFillConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0].GetType().IsValueType && values[1].GetType().IsValueType && values[2].GetType().IsValueType && values[3].GetType().IsValueType && values[4].GetType().IsValueType && values[5].GetType().IsValueType)
		{
			Point point = (Point)values[0];
			double num = (double)values[1];
			double num2 = (double)values[2];
			double num3 = (double)values[3];
			double num4 = (double)values[4];
			Point point2 = (Point)values[5];
			return new Point(num * (point.X - point2.X) / num3, num2 * (point.Y - point2.Y) / num4);
		}
		return default(Point);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
