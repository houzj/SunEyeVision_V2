using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Commom;

public class Brush2ColorConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is SolidColorBrush { Color: var color })
		{
			return color.R.ToString().PadLeft(3, ' ') + "," + color.G.ToString().PadLeft(3, ' ') + "," + color.B.ToString().PadLeft(3, ' ');
		}
		return Colors.Black;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Convert(value, targetType, parameter, culture);
	}
}
