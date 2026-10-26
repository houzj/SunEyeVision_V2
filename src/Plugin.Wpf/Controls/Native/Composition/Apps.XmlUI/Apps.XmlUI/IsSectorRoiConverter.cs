using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class IsSectorRoiConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		if (text == "Circle" || text == "Sector")
		{
			return true;
		}
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
