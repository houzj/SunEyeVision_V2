using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility.Convert;

public class LeftMarginMultiplierConverter : IValueConverter
{
	public double Length { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(value is TreeViewItem item))
		{
			return new Thickness(0.0);
		}
		return new Thickness(Length * (double)item.GetDepth(), 0.0, 0.0, 0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
