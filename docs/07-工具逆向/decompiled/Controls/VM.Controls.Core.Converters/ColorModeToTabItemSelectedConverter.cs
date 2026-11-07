using System;
using System.Globalization;
using System.Windows.Data;

namespace VM.Controls.Core.Converters;

public class ColorModeToTabItemSelectedConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((ColorMode)value != ColorMode.ColorPalette) ? 1 : 0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return ((int)value != 0) ? ColorMode.ColorCanvas : ColorMode.ColorPalette;
	}
}
