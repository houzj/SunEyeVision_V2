using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base;

internal class PointCvt : IValueConverter
{
	public static PointCvt Cvt = new PointCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is Point point)
		{
			if (parameter.ToString() == "X")
			{
				return point.X;
			}
			if (parameter.ToString() == "Y")
			{
				return point.Y;
			}
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
