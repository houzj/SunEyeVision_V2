using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Converters;

public class RoundedValueConverter : IValueConverter
{
	private int _precision;

	public int Precision
	{
		get
		{
			return _precision;
		}
		set
		{
			_precision = value;
		}
	}

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is double)
		{
			return Math.Round((double)value, _precision);
		}
		if (value is Point point)
		{
			return new Point(Math.Round(point.X, _precision), Math.Round(((Point)value).Y, _precision));
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return value;
	}
}
