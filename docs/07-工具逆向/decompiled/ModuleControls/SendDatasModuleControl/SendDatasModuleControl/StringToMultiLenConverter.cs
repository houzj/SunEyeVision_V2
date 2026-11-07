using System;
using System.Globalization;
using System.Windows.Data;
using Apps.Localization;

namespace SendDatasModuleControl;

public class StringToMultiLenConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return "";
		}
		string result = "";
		if (value is string infoKey)
		{
			result = AppLocalizationService.GetLang(infoKey);
			result = result.Replace("\\r\\n", "\r\n");
		}
		return result;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
