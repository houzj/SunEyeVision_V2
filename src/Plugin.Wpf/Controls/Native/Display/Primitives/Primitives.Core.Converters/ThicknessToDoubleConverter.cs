using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Converters;

public class ThicknessToDoubleConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double num = 1.0;
		if (value != null)
		{
			num = ((Thickness)value).Top;
		}
		return num;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		double uniformLength = 1.0;
		if (value != null)
		{
			uniformLength = (double)value;
		}
		return new Thickness(uniformLength);
	}
}
