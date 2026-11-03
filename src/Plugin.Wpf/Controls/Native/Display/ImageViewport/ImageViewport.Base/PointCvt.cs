using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.Base;

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
