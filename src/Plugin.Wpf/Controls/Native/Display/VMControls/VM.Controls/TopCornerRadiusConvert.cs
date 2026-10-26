using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

internal class TopCornerRadiusConvert : IValueConverter
{
	private readonly CornerRadiusConverter _cornerRadiusConverter = new CornerRadiusConverter();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return default(CornerRadius);
		}
		CornerRadius cornerRadius = (CornerRadius)value;
		return new CornerRadius(cornerRadius.TopLeft, cornerRadius.TopRight, 0.0, 0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		if (text == null)
		{
			return default(CornerRadius);
		}
		return _cornerRadiusConverter.ConvertFromString(text);
	}
}
