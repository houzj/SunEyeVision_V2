using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public class StringToMultiLenConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return "";
		}
		string text = "";
		if (value is string text2)
		{
			text = text2;
			int num = 80;
			for (int i = num; i < text.Length; i += num + 1)
			{
				text = text.Insert(i, "\r\n");
			}
		}
		return text;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
