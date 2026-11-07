using System;
using System.Globalization;
using System.Windows.Data;

namespace Apps.XmlParser.ParamTab;

public class IntegerValueConvert : IValueConverter
{
	public object Convert(object value, Type t, object param, CultureInfo cultureInfo)
	{
		return (value != null) ? value.ToString() : "0";
	}

	public object ConvertBack(object value, Type t, object param, CultureInfo cultureInfo)
	{
		string s = $"{value}";
		int.TryParse(s, out var result);
		return result;
	}
}
