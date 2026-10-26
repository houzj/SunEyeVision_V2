using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;

[ValueConversion(typeof(bool), typeof(Visibility))]
public class BtnShowGridConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (System.Convert.ToBoolean(value))
		{
			return 1;
		}
		return 2;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
