using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Core;

public class IsBoxRoiConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		return text == "Rect";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
