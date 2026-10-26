using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF;

public class LogManageTypeImageConvert : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if ("错误" == (string)value)
		{
			return new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/ic_log_错误.png", UriKind.Relative));
		}
		if ("警告" == (string)value)
		{
			return new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/ic_log_告警.png", UriKind.Relative));
		}
		if ("信息" == (string)value)
		{
			return new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/ic_log_信息.png", UriKind.Relative));
		}
		return new BitmapImage(new Uri("/Apps.XmlUI;component/Resources/ic_log_信息.png", UriKind.Relative));
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		throw new NotImplementedException();
	}
}
