using System;
using System.Globalization;
using System.Net;
using System.Windows.Data;

namespace VisionMasterMain.AppMainWindow;

public class IpLegalCvt : IValueConverter
{
	public static IpLegalCvt Cvt = new IpLegalCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		IPAddress address;
		return IPAddress.TryParse(value?.ToString(), out address);
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
