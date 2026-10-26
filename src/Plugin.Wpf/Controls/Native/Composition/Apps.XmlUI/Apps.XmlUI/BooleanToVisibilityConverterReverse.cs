using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class BooleanToVisibilityConverterReverse : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if ((bool)value)
		{
			return Visibility.Collapsed;
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if ((Visibility)value == Visibility.Visible)
		{
			return false;
		}
		return true;
	}
}
