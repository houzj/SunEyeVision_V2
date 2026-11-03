using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public class UserTempConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((double)value).ToString("F3");
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			return System.Convert.ToDouble(value);
		}
		catch
		{
			return null;
		}
	}
}
