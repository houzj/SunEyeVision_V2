using System;
using System.Globalization;
using System.Windows.Data;

namespace VisionMasterMain.AppMainWindow;

public class IpAssignmentEnumCvt : IValueConverter
{
	public const int StaticIP = 0;

	public const int DHCP = 1;

	public const int LLA = 2;

	public static IpAssignmentEnumCvt Cvt = new IpAssignmentEnumCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is ModifyIpWindow.IpAssignmentEnum ipAssignmentEnum && true && int.TryParse(parameter?.ToString(), out var result))
		{
			switch (ipAssignmentEnum)
			{
			case ModifyIpWindow.IpAssignmentEnum.StaticIP:
				return result == 0;
			case ModifyIpWindow.IpAssignmentEnum.DHCP:
				return result == 1;
			case ModifyIpWindow.IpAssignmentEnum.LLA:
				return result == 2;
			}
		}
		return value;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (bool.TryParse(value?.ToString(), out var result) && result && int.TryParse(parameter?.ToString(), out var result2))
		{
			switch (result2)
			{
			case 0:
				return ModifyIpWindow.IpAssignmentEnum.StaticIP;
			case 1:
				return ModifyIpWindow.IpAssignmentEnum.DHCP;
			case 2:
				return ModifyIpWindow.IpAssignmentEnum.LLA;
			}
		}
		return Binding.DoNothing;
	}
}
