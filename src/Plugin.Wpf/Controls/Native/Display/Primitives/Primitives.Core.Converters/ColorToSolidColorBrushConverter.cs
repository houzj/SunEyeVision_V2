using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Converters;

public class ColorToSolidColorBrushConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			return new SolidColorBrush((Color)value);
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			return ((SolidColorBrush)value).Color;
		}
		return value;
	}
}
