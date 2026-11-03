using SunEyeVision.Plugin.Wpf.Native.Input.FieldPrimitives.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Spinners;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Spinners;

[ValueConversion(typeof(double), typeof(double))]
public class BtnHeightConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (double?)value / 2.0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
