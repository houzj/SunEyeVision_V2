using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Reflection;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility.Convert;

public class ToolTipShowConvert : IValueConverter
{
	public string FontFamily = "Microsoft YaHei";

	public double FontSize = 12.0;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return false;
		}
		if (value is string)
		{
			MeasureText measureText = new MeasureText(value.ToString(), FontSize, FontFamily);
			if (parameter == null)
			{
				return false;
			}
			double num = System.Convert.ToDouble(parameter);
			return measureText.Width >= num;
		}
		PropertyInfo propertyInfo = value.GetType().GetProperty("Text") ?? value.GetType().GetProperty("Content") ?? value.GetType().GetProperty("Header");
		if (propertyInfo == null)
		{
			return false;
		}
		string text = propertyInfo.GetValue(value, null) as string;
		if (string.IsNullOrEmpty(text))
		{
			return false;
		}
		PropertyInfo property = value.GetType().GetProperty("FontSize");
		if (property == null)
		{
			return false;
		}
		double fontSize = (double)property.GetValue(value, null);
		PropertyInfo property2 = value.GetType().GetProperty("FontFamily");
		if (property2 == null)
		{
			return false;
		}
		string fontFamily = property2.GetValue(value, null).ToString();
		MeasureText measureText2 = new MeasureText(text, fontSize, fontFamily);
		if (parameter == null)
		{
			return false;
		}
		double num2 = System.Convert.ToDouble(parameter);
		return measureText2.Width >= num2;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
