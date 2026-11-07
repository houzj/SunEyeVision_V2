using System;
using System.Globalization;
using System.Windows.Data;

namespace VM.Controls.Core.Converters;

public class AdditionConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null && parameter != null)
		{
			double num = (double)value;
			double num2 = double.Parse(parameter as string);
			return num + num2;
		}
		return 0.0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
