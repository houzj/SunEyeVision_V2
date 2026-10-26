using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

public class ItemBackgroundConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEBEB"));
		}
		int result = 0;
		int.TryParse(value.ToString(), out result);
		if (result % 2 == 0)
		{
			return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D8D8D8"));
		}
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBEBEB"));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
