using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public class Hex2ColorConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (SolidColorBrush)new BrushConverter().ConvertFrom(value);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (value as SolidColorBrush).Color.ToString();
	}
}
