using System;
using System.Globalization;
using System.Windows.Data;

namespace IMVSHPFeatureMatchModuControl;

public class SelectedIndexToBoolConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			if (value == null)
			{
				return 1;
			}
			if (!(bool)value)
			{
				return 2;
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
				return null;
			}
			if ((int)value == 2)
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
