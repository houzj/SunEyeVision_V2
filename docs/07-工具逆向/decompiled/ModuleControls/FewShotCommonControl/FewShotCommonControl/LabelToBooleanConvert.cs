using System;
using System.Globalization;
using System.Windows.Data;

namespace FewShotCommonControl;

public class LabelToBooleanConvert : IValueConverter
{
	public object Convert(object value, Type type, object param, CultureInfo c)
	{
		string value2 = (string)value;
		return string.IsNullOrWhiteSpace(value2);
	}

	public object ConvertBack(object value, Type type, object param, CultureInfo c)
	{
		throw new NotImplementedException();
	}
}
