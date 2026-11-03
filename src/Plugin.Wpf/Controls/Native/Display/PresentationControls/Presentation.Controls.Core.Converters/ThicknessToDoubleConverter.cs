using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core.Converters;

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
