using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace VM.Controls.Core.Converters;

public class BorderThicknessToStrokeThicknessConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		Thickness thickness = (Thickness)value;
		return (thickness.Bottom + thickness.Left + thickness.Right + thickness.Top) / 4.0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		int? num = (int?)value;
		int num2 = (num.HasValue ? num.Value : 0);
		return new Thickness(num2, num2, num2, num2);
	}
}
