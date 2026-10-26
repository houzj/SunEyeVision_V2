using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

[ValueConversion(typeof(double), typeof(string))]
public class DecimationConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return null;
		}
		double num = (double)value;
		return (parameter != null) ? num.ToString("F" + (uint)parameter) : null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			return System.Convert.ToDouble(value);
		}
		catch (Exception)
		{
			return value;
		}
	}
}
