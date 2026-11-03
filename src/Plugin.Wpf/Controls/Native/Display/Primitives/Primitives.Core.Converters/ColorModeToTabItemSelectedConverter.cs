using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Converters;

public class ColorModeToTabItemSelectedConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((ColorMode)value != ColorMode.ColorPalette) ? 1 : 0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((int)value != 0) ? ColorMode.ColorCanvas : ColorMode.ColorPalette;
	}
}
