using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class IsCaliperRoiConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		string text = (string)value;
		switch (text)
		{
		default:
			if (!(text == "MatrixCircle"))
			{
				return false;
			}
			goto case "CircleCaliper";
		case "CircleCaliper":
		case "SectorCaliper":
		case "LineCaliper":
			return true;
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return null;
	}
}
