using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Apps.Data;

namespace FormatModuleControl;

public class ThicknessConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is ItemState itemState)
		{
			return itemState switch
			{
				ItemState.GetParamErro => (object)new Thickness(0.0), 
				ItemState.SetParamErro => new Thickness(1.0), 
				_ => new Thickness(0.0), 
			};
		}
		return new Thickness(0.0);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
