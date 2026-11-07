using System;
using System.Globalization;
using System.Windows.Data;
using Apps.Localization;

namespace FewShotCommonControl;

public class BooleanToStringConvert : IValueConverter
{
	public object Convert(object value, Type type, object param, CultureInfo c)
	{
		if (value != null)
		{
			string text = (string)value;
			if (text == "False")
			{
				return AppLocalizationService.GetLang("Off");
			}
			return AppLocalizationService.GetLang("Open");
		}
		return "";
	}

	public object ConvertBack(object value, Type type, object param, CultureInfo c)
	{
		throw new NotImplementedException();
	}
}
