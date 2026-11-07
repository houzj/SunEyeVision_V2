using System;
using System.Globalization;
using System.Windows.Data;

namespace Apps.XmlParser.ParamTab;

public class GrayMatchVADialogDataCvt : IValueConverter
{
	public const string Index = "Index";

	public const string Enable = "Enable";

	public static GrayMatchVADialogDataCvt Cvt = new GrayMatchVADialogDataCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (int.TryParse(value?.ToString(), out var result))
		{
			string text = parameter?.ToString();
			if (text == "Index")
			{
				return (result == 0) ? 1 : 0;
			}
			if (text == "Enable")
			{
				return result == 0;
			}
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = parameter?.ToString();
		if (text == "Index")
		{
			if (int.TryParse(value?.ToString(), out var result))
			{
				return (result == 0) ? 1 : 0;
			}
			return value;
		}
		return Binding.DoNothing;
	}
}
