using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Platform;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView;

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
