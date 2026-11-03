using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Platform.Convert;
using SunEyeVision.Plugin.Wpf.Converters.Tree;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Converters.Tree;

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
