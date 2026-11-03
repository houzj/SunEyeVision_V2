using SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Arranged;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Editors;

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
