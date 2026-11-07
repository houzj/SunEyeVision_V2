using System;
using System.Globalization;
using System.Windows.Data;

namespace VMControls.WPF.GlobalUI;

public class StringToTootipConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return "";
		}
		string result = "";
		if (value is string text)
		{
			result = text.Replace(";", "\r\n");
		}
		return result;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
