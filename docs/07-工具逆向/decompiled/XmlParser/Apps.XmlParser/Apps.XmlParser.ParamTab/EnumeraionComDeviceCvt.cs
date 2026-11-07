using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace Apps.XmlParser.ParamTab;

public class EnumeraionComDeviceCvt : IValueConverter
{
	public static EnumeraionComDeviceCvt Cvt = new EnumeraionComDeviceCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is IList list && int.TryParse(parameter?.ToString(), out var result))
		{
			IList list2 = new List<EnumEntry>();
			foreach (EnumEntry item in list)
			{
				if (item.DeviceType == result)
				{
					list2.Add(item);
				}
			}
			return list2;
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
