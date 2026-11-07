using System;
using System.Globalization;
using System.Windows.Data;

namespace IMVSHPFeatureMatchModuControl;

public class ModeToEnableCvt : IValueConverter
{
	public static ModeToEnableCvt Cvt = new ModeToEnableCvt();

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
