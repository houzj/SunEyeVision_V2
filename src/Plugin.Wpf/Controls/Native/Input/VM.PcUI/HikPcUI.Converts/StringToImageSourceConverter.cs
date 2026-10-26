using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Converts;

public class StringToImageSourceConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		if (!string.IsNullOrEmpty(text))
		{
			return new BitmapImage(new Uri(text, UriKind.RelativeOrAbsolute));
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
