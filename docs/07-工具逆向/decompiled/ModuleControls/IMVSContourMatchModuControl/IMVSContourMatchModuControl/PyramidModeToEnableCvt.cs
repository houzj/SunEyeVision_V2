using System;
using System.Globalization;
using System.Windows.Data;

namespace IMVSContourMatchModuControl;

public class PyramidModeToEnableCvt : IValueConverter
{
	public static PyramidModeToEnableCvt Cvt = new PyramidModeToEnableCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (int.TryParse(value.ToString(), out var result))
		{
			return result == 0;
		}
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
