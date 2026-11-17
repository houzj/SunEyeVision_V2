using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using Apps.Localization;
using Apps.Log;
using VM.Controls;

namespace VisionMasterMain.AppMainWindow;

public partial class ModifyIpWindow : BaseWindow, IComponentConnector
{
	public enum IpAssignmentEnum
	{
		StaticIP = 83886080,
		DHCP = 100663296,
		LLA = 67108864
	}

	public static readonly DependencyProperty CurrentDeviceProperty = DependencyProperty.Register("CurrentDevice", typeof(DeviceInfo), typeof(ModifyIpWindow), new PropertyMetadata(null));

	public static readonly DependencyProperty SelectedIpAssignmentProperty = DependencyProperty.Register("SelectedIpAssignment", typeof(IpAssignmentEnum), typeof(ModifyIpWindow), new PropertyMetadata(IpAssignmentEnum.StaticIP));

	public static readonly DependencyProperty ProgressBarVisibilityProperty = DependencyProperty.Register("ProgressBarVisibility", typeof(Visibility), typeof(ModifyIpWindow), new PropertyMetadata(Visibility.Hidden));

	public DeviceInfo CurrentDevice
	{
		get
		{
			return (DeviceInfo)GetValue(CurrentDeviceProperty);
		}
		set
		{
			SetValue(CurrentDeviceProperty, value);
		}
	}

	public IpAssignmentEnum SelectedIpAssignment
	{
		get
		{
			return (IpAssignmentEnum)GetValue(SelectedIpAssignmentProperty);
		}
		set
		{
			SetValue(SelectedIpAssignmentProperty, value);
		}
	}

	public Visibility ProgressBarVisibility
	{
		get
		{
			return (Visibility)GetValue(ProgressBarVisibilityProperty);
		}
		set
		{
			SetValue(ProgressBarVisibilityProperty, value);
		}
	}

	private IEnumerable<string> EnumIpList { get; set; }

	public ModifyIpWindow(DeviceInfo deviceInfo, IEnumerable<string> enumIpList)
	{
		InitializeComponent();
		CurrentDevice = deviceInfo;
		EnumIpList = enumIpList;
		string addressIP = GetAddressIP();
		if (addressIP != null)
		{
			uint ipUintFromString = addressIP.GetIpUintFromString();
			uint maxIPStrFromUint = addressIP.GetMaxIPStrFromUint(CurrentDevice.SubNetMask);
			uint ipUintFromString2 = CurrentDevice.SubNetMask.GetIpUintFromString();
			uint ip = ipUintFromString & ipUintFromString2;
			CurrentDevice.LowIpAddress = ip.GetLowIPStrFromUint();
			CurrentDevice.HighIpAddress = maxIPStrFromUint.GetIPStrFromUint();
		}
	}

	private bool CheckIpLegal(params string[] ips)
	{
		foreach (string ipString in ips)
		{
			if (!IPAddress.TryParse(ipString, out var _))
			{
				return false;
			}
		}
		return true;
	}

	private string GetAddressIP()
	{
		IPAddress[] addressList = Dns.GetHostEntry(Dns.GetHostName()).AddressList;
		foreach (IPAddress iPAddress in addressList)
		{
			if (iPAddress.AddressFamily.ToString() == "InterNetwork")
			{
				return iPAddress.ToString();
			}
		}
		return null;
	}

	private bool CheckIpRange()
	{
		string addressIP = GetAddressIP();
		if (addressIP == null)
		{
			return false;
		}
		uint ipUintFromString = addressIP.GetIpUintFromString();
		uint ipUintFromString2 = CurrentDevice.SubNetMask.GetIpUintFromString();
		uint maxIPStrFromUint = addressIP.GetMaxIPStrFromUint(CurrentDevice.SubNetMask);
		uint num = ipUintFromString & ipUintFromString2;
		CurrentDevice.LowIpAddress = num.GetLowIPStrFromUint();
		CurrentDevice.HighIpAddress = maxIPStrFromUint.GetIPStrFromUint();
		uint ipUintFromString3 = CurrentDevice.CurrentIP.GetIpUintFromString();
		uint ipUintFromString4 = CurrentDevice.DefaultGateway.GetIpUintFromString();
		if (ipUintFromString3 < num || ipUintFromString3 > maxIPStrFromUint)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("ModiyfyIpAddressTips"), MessageIcons.Warning);
			return false;
		}
		if (ipUintFromString4 < num || ipUintFromString4 > maxIPStrFromUint)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("ModiyfyDefaultGatewayTips"), MessageIcons.Warning);
			return false;
		}
		return true;
	}

	private bool IsIpConflicts()
	{
		IEnumerable<string> enumIpList = EnumIpList;
		if (enumIpList.Contains(CurrentDevice.CurrentIP))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("ModiyfyIpAddresRepeatTips"), MessageIcons.Warning);
			return false;
		}
		return true;
	}

	private async Task<bool> IsNotOccupied(string connectionIp)
	{
		bool isNotOccupied = true;
		Ping p = new Ping();
		try
		{
			if ((await Task.Run(() => p.Send(connectionIp, 3000))).Status == IPStatus.Success)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("ModiyfyIpAddresOccupiedTips"), MessageIcons.Warning);
				isNotOccupied = false;
			}
		}
		catch (Exception)
		{
			isNotOccupied = true;
		}
		return isNotOccupied;
	}

	private async Task<bool> StaticIPEnumCheck()
	{
		if (IsIpConflicts() && CheckIpRange())
		{
			string ip = CurrentDevice?.CurrentIP;
			return await IsNotOccupied(ip);
		}
		return false;
	}

	private async void ConfigButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (!CheckIpLegal(CurrentDevice.CurrentIP, CurrentDevice.SubNetMask, CurrentDevice.DefaultGateway))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("InputIpAddressTips"), MessageIcons.Warning);
				return;
			}
			ProgressBarVisibility = Visibility.Visible;
			bool flag = SelectedIpAssignment == IpAssignmentEnum.StaticIP;
			bool flag2 = flag;
			if (flag2)
			{
				flag2 = await StaticIPEnumCheck();
			}
			if (flag2 || SelectedIpAssignment != IpAssignmentEnum.StaticIP)
			{
				DeviceInfo currentDevice = CurrentDevice;
				int selectedIpAssignment = (int)SelectedIpAssignment;
				await Task.Run(() =>
				{
					currentDevice.SetDeviceMode(selectedIpAssignment);
				});
				ProgressBarVisibility = Visibility.Hidden;
				LogHelper.Info("IP修改成功");
				HikMessageBox.Show(AppLocalizationService.GetLang("NodifyIpSucceeded"), MessageIcons.Ok);
				DialogResult = true;
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			LogHelper.Error($"IP修改失败：{ex2.Message},{ex2.StackTrace?.ToString()}");
			HikMessageBox.Show(string.Format(string.Format("{0}", AppLocalizationService.GetLang("NodifyIpFailed")), ex2.Message), MessageIcons.Error);
		}
		finally
		{
			ProgressBarVisibility = Visibility.Hidden;
		}
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		Close_OnClick(sender, e);
	}
}
