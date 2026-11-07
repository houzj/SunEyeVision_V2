using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Apps.Data;

namespace FormatModuleControl;

public class FormatBackGroundConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is ItemState itemState)
		{
			return itemState switch
			{
				ItemState.GetParamErro => (object)new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4EBEB")), 
				ItemState.SetParamErro => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F4EBEB")), 
				_ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECEC")), 
			};
		}
		return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#ECECEC"));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
