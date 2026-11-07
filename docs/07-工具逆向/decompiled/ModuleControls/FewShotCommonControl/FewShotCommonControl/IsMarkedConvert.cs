using System;
using System.Globalization;
using System.Windows.Data;

namespace FewShotCommonControl;

public class IsMarkedConvert : IValueConverter
{
	public object Convert(object value, Type type, object param, CultureInfo c)
	{
		string text = (string)value;
		return text == "已标为此类";
	}

	public object ConvertBack(object value, Type type, object param, CultureInfo c)
	{
		throw new NotImplementedException();
	}
}
