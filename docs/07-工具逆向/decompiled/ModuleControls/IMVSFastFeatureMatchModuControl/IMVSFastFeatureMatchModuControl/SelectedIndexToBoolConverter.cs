using System;
using System.Globalization;
using System.Windows.Data;

namespace IMVSFastFeatureMatchModuControl;

public class SelectedIndexToBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (!(bool)value)
			{
				return 1;
			}
			return 0;
		}
		catch
		{
			throw new NotImplementedException();
		}
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if ((int)value == 1)
			{
				return false;
			}
			return true;
		}
		catch
		{
			throw new NotImplementedException();
		}
	}
}
