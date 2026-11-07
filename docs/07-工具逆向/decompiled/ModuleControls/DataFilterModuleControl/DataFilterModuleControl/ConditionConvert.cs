using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Apps.XmlParser.ParamTab;

namespace DataFilterModuleControl;

public class ConditionConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is ConditionGroupItem)
		{
			return Visibility.Visible;
		}
		return Visibility.Collapsed;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
