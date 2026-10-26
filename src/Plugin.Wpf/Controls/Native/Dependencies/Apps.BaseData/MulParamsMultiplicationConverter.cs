using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

public class MulParamsMultiplicationConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values[0] == DependencyProperty.UnsetValue)
		{
			return null;
		}
		return values.ToList();
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
