using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility.Convert;

public class LongTextConvert : IValueConverter
{
	public int FontSize { get; set; }

	public int MaxLen { get; set; }

	public string FontFamily { get; set; }

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = value as string;
		if (string.IsNullOrEmpty(text))
		{
			return string.Empty;
		}
		string text2 = parameter as string;
		int result;
		int result2;
		string text3;
		if (!string.IsNullOrEmpty(text2))
		{
			string[] array = text2.Split(',');
			if (array.Length != 3)
			{
				return text;
			}
			if (!int.TryParse(array[0], out result))
			{
				return text;
			}
			if (!int.TryParse(array[1], out result2))
			{
				return text;
			}
			text3 = array[2];
		}
		else
		{
			result = FontSize;
			result2 = MaxLen;
			text3 = FontFamily;
		}
		if (result < 1 || result2 < 1 || string.IsNullOrEmpty(text3))
		{
			return text;
		}
		MeasureText measureText = new MeasureText(text, result, text3);
		int num = text.Length;
		while (measureText.Width > (double)result2)
		{
			num--;
			measureText = new MeasureText(text.Substring(0, num) + "...", result, text3);
		}
		if (num != text.Length)
		{
			return text.Substring(0, num) + "...";
		}
		return text;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
