using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

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
