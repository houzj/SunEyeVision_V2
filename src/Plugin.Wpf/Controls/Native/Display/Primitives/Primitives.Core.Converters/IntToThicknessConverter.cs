using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core.Converters;

public class IntToThicknessConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		int num = 0;
		if (value != null)
		{
			num = (int)value;
		}
		if (parameter != null && parameter.ToString().ToUpper() == "LEFT")
		{
			return new Thickness(num, 0.0, 0.0, 0.0);
		}
		return new Thickness(num);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
