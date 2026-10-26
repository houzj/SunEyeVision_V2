using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BtnShowConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (System.Convert.ToBoolean(value))
		{
			return Visibility.Visible;
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
