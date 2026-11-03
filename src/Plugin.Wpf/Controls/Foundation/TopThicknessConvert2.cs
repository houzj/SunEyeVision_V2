using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Controls.Foundation;

internal class TopThicknessConvert2 : IValueConverter
{
	private readonly ThicknessConverter _thicknessConverter = new ThicknessConverter();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return default(Thickness);
		}
		Thickness thickness = (Thickness)value;
		return new Thickness(thickness.Left, 0.0, thickness.Right, thickness.Bottom);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		if (text == null)
		{
			return default(Thickness);
		}
		return _thicknessConverter.ConvertFromString(text);
	}
}
