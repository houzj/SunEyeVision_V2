using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

public class IntToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type TargetType, object parameter, CultureInfo culture)
	{
		if (int.Parse(value.ToString()) > 0)
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
