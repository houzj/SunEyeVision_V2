using System;
using System.Globalization;
using System.Windows.Data;

namespace IMVSMarkInspModuControl;

public class TrainCountConvert : IValueConverter
{
	public object Convert(object value, Type type, object param, CultureInfo c)
	{
		int num = (int)value;
		return num > 0;
	}

	public object ConvertBack(object value, Type type, object param, CultureInfo c)
	{
		throw new NotImplementedException();
	}
}
