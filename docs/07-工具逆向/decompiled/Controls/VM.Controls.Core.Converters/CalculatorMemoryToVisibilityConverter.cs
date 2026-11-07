using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VM.Controls.Core.Converters;

public class CalculatorMemoryToVisibilityConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((decimal)value == 0m) ? Visibility.Hidden : Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
