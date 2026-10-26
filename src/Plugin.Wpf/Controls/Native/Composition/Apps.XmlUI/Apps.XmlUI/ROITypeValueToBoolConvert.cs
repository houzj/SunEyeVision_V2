using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class ROITypeValueToBoolConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (value.ToString() == parameter.ToString())
			{
				return true;
			}
		}
		catch (Exception)
		{
		}
		return false;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
