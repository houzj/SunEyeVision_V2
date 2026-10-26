using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

public class FullPathConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = ((value == null) ? "" : value.ToString().Replace(".", ">"));
		int num = text.LastIndexOf('>');
		if (num > 0 && num < text.Length - 1)
		{
			return text.Remove(num + 1);
		}
		return "";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
