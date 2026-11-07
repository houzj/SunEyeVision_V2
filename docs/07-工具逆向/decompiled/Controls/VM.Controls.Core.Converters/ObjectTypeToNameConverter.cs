using System;
using System.Globalization;
using System.Windows.Data;

namespace VM.Controls.Core.Converters;

public class ObjectTypeToNameConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value != null)
		{
			string text = value.ToString();
			if (string.IsNullOrEmpty(text) || text == value.GetType().UnderlyingSystemType.ToString())
			{
				return value.GetType().Name;
			}
			return value;
		}
		return null;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
