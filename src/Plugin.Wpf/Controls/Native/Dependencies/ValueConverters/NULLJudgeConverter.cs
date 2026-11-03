using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.ValueConverters;

public class NULLJudgeConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value == null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
