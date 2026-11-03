using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView;

public class Bool2CollapesdConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			bool flag = false;
			if (value is bool)
			{
				return ((bool)value) ? Visibility.Collapsed : Visibility.Visible;
			}
		}
		return Visibility.Visible;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
