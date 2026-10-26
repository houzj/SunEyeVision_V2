using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class FgConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (!(bool)value)
		{
			return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF808080"));
		}
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC0C0C0"));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
