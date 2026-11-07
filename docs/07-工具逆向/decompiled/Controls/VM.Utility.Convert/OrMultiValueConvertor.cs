using System;
using System.Globalization;
using System.Windows.Data;

namespace VM.Utility.Convert;

public class OrMultiValueConvertor : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		try
		{
			foreach (object obj in values)
			{
				if (obj.Equals(true))
				{
					return obj;
				}
			}
			return false;
		}
		catch (Exception)
		{
			return false;
		}
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
