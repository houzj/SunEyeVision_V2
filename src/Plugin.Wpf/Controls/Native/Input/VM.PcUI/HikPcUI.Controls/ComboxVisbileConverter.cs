using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public class ComboxVisbileConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			if (System.Convert.ToInt32(value) <= 1)
			{
				return Visibility.Collapsed;
			}
			return Visibility.Visible;
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
