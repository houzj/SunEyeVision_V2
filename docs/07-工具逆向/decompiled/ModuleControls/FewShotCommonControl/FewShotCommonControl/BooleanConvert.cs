using System;
using System.Globalization;
using System.Windows.Data;
using Apps.Localization;

namespace FewShotCommonControl;

public class BooleanConvert : IValueConverter
{
	public object Convert(object value, Type type, object param, CultureInfo c)
	{
		if (value != null)
		{
			string text = (string)value;
			if (text == "False")
			{
				return AppLocalizationService.GetLang("False");
			}
			return AppLocalizationService.GetLang("True");
		}
		return "";
	}

	public object ConvertBack(object value, Type type, object param, CultureInfo c)
	{
		throw new NotImplementedException();
	}
}
