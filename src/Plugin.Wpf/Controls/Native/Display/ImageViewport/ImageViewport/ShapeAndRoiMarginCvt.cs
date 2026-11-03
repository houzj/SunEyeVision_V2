using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.View.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.ViewUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport;

public class ShapeAndRoiMarginCvt : IValueConverter
{
	public static ShapeAndRoiMarginCvt Cvt;

	public bool ImageROIOffsetEnable { get; set; }

	static ShapeAndRoiMarginCvt()
	{
		Cvt = new ShapeAndRoiMarginCvt();
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (ImageROIOffsetEnable && double.TryParse(value?.ToString(), out var result))
		{
			double num = result * 0.5;
			return new Thickness(num, num, 0.0, 0.0);
		}
		return new Thickness(0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
