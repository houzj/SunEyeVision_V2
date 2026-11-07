using System;
using System.Globalization;
using System.Windows.Data;

namespace ShellModuleControl;

public class ButtonEnableConverter : IMultiValueConverter
{
	public static ButtonEnableConverter Cvt = new ButtonEnableConverter();

	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (int.TryParse(values[0]?.ToString(), out var result))
		{
			if (parameter?.ToString() == "Up")
			{
				return result >= 1;
			}
			if (parameter?.ToString() == "Down" && int.TryParse(values[1]?.ToString(), out var result2))
			{
				return result != result2 - 1;
			}
		}
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		return null;
	}
}
