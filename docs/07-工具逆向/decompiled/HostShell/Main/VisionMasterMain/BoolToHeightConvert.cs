using System;
using System.Globalization;
using System.Windows.Data;

namespace VisionMasterMain;

public class BoolToHeightConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value == null)
		{
			return 40;
		}
		if (value is bool flag)
		{
			return flag ? 40 : 0;
		}
		return 0;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
