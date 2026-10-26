using System;
using System.Globalization;
using System.Text;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI;

public class EqualConverter : IMultiValueConverter
{
	public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
	{
		if (values != null)
		{
			if (values.Length > 1 && values[0] is int && values[1] is int)
			{
				int num = (int)values[0];
				int num2 = (int)values[1];
				return num == num2;
			}
			StringBuilder stringBuilder = new StringBuilder("EqualConverter:");
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i] == null)
				{
					stringBuilder.Append("null");
				}
				else
				{
					stringBuilder.Append(values[i].ToString());
				}
				stringBuilder.Append(",");
			}
			return false;
		}
		return false;
	}

	public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
