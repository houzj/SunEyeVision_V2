using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class GroupInfoConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		int result = 0;
		int.TryParse(parameter.ToString(), out result);
		if (value is string)
		{
			string[] array = ((string)value).Split(';');
			if (array.Length > result)
			{
				return array[result];
			}
		}
		return "";
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
