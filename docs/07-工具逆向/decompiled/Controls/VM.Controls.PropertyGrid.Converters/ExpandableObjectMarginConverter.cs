using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VM.Controls.PropertyGrid.Converters;

public class ExpandableObjectMarginConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return new Thickness((int)value * 15, 0.0, 0.0, 0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
