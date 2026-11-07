using System;
using System.Globalization;
using System.Windows.Data;

namespace Apps.XmlParser.ParamTab;

public class WidthConver : IValueConverter
{
	private double div = 3.0;

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return (double)value - div;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
