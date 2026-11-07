using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Hik.Util.Security;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;

namespace VisionMasterMain.AppMainWindow.SoftwareSet;

public partial class RemoteServerVerification : BaseWindow, IComponentConnector
{
	public delegate void UpdateSolutionPath();

	private Visibility _progressBarVisibility = Visibility.Hidden;

	private bool _isIpVerifycation = false;

	private string _serverIp = "127.0.0.1";

	private uint _serverPort = 5556u;

	private string _passwordTips = "";

	private bool _isLegalIP = true;

	private bool _isOpenNativeMode = true;

	private int waitingTime = 0;

	public Visibility ProgressBarVisibility
	{
		get
		{
			return _progressBarVisibility;
		}
		set
		{
			SetPropertyChanged(ref _progressBarVisibility, value, "ProgressBarVisibility");
			if (value == Visibility.Visible)
			{
				RemoteServerWindow.IsEnabled = false;
				LocalModeButton.IsEnabled = false;
				ConfirmButton.IsEnabled = false;
			}
			else
			{
				RemoteServerWindow.IsEnabled = true;
				LocalModeButton.IsEnabled = true;
				ConfirmButton.IsEnabled = true;
			}
		}
	}

	public bool IsIpVerifycation
	{
		get
		{
			return _isIpVerifycation;
		}
		set
		{
			SetPropertyChanged(ref _isIpVerifycation, value, "IsIpVerifycation");
		}
	}

	public string ServerIp
	{
		get
		{
			return _serverIp;
		}
		set
		{
			SetPropertyChanged(ref _serverIp, value, "ServerIp");
		}
	}

	public uint ServerPort
	{
		get
		{
			return _serverPort;
		}
		set
		{
			SetPropertyChanged(ref _serverPort, value, "ServerPort");
		}
	}

	public string PasswordTips
	{
		get
		{
			return _passwordTips;
		}
		set
		{
			SetPropertyChanged(ref _passwordTips, value, "PasswordTips");
		}
	}

	public bool IsLegalIP
	{
		get
		{
			return _isLegalIP;
		}
		set
		{
			SetPropertyChanged(ref _isLegalIP, value, "IsLegalIP");
		}
	}

	public bool IsOpenNativeMode
	{
		get
		{
			return _isOpenNativeMode;
		}
		set
		{
			SetPropertyChanged(ref _isOpenNativeMode, value, "IsOpenNativeMode");
		}
	}

	private static int PasswordErrorCounter { get; set; }

	public event UpdateSolutionPath NotifySolutionPathChanged;

	public RemoteServerVerification()
	{
		InitializeComponent();
		LoadServerSetting();
	}

	private void LoadServerSetting()
	{
		string config = ConfigurationHelper.GetConfig("ServerSetting");
		if (!string.IsNullOrEmpty(config))
		{
			if (config.Contains("127.0.0.1"))
			{
				config = ConfigurationHelper.GetConfig("LastRunServerSetting");
			}
			string[] array = config?.Split(':');
			if (array != null && array.Length == 2)
			{
				ServerIp = array[0];
				if (uint.TryParse(array[1], out var result))
				{
					ServerPort = result;
				}
			}
		}
		if (bool.TryParse(ConfigurationHelper.GetConfig("IsOpenNativeMode"), out var result2))
		{
			IsOpenNativeMode = result2;
		}
	}

	private void SaveServerSetting(string server)
	{
		string config = ConfigurationHelper.GetConfig("ServerSetting");
		if (!string.IsNullOrEmpty(config) && !config.Contains("127.0.0.1"))
		{
			ConfigurationHelper.SaveConfig("LastRunServerSetting", config);
		}
		ConfigurationHelper.SaveConfig("ServerSetting", server);
		string defaultValue = RsaCryptHelper.GetInstance().Encryt(InputPasswordBox.Password);
		ConfigurationHelper.SaveConfig("ServerPassword", defaultValue);
	}

	private async Task<uint> ReconnectServer(string server)
	{
		uint status = await Task.Run(() => CVmObjectBase.ReconnectServer(server, ref waitingTime));
		LogHelper.Info($"CVmObjectBase.ReconnectServer返回值：{$"0x{status:x2}"}");
		if (status == 0)
		{
			SaveServerSetting(server);
		}
		return status;
	}

	private async void VerificationButton_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			RemoteServerWindow.IsEnabled = false;
			IsLegalIP = IPAddress.TryParse(ServerIp, out var _);
			if (!IsLegalIP)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("IPLegalTips"), MessageIcons.Warning);
				return;
			}
			ProgressBarVisibility = Visibility.Visible;
			string ip = ServerIp;
			uint port = ServerPort;
			uint time = 0u;
			uint serverStatus = await Task.Run(() => (uint)ServerSDKAPI.CheckServerPasswordStatus(ip.Trim(), port, ref time));
			LogHelper.Info($"ServerSDKAPI.CheckServerPasswordStatus返回值：{$"0x{serverStatus:x2}"}");
			ProgressBarVisibility = Visibility.Hidden;
			switch (serverStatus)
			{
			case 3758098434u:
				InputTextBlock.Text = AppLocalizationService.GetLang("Lang_SetScriptPassword");
				ConfirmButton.IsEnabled = true;
				InputStackPanel.Visibility = Visibility.Visible;
				ConfirmStackPanel.Visibility = Visibility.Visible;
				VerificationStackPanel.Visibility = Visibility.Collapsed;
				break;
			case 3758098437u:
				InputTextBlock.Text = AppLocalizationService.GetLang("输入密码");
				ConfirmButton.IsEnabled = true;
				InputStackPanel.Visibility = Visibility.Visible;
				VerificationStackPanel.Visibility = Visibility.Collapsed;
				break;
			default:
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo((int)serverStatus), MessageIcons.Error);
				break;
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			HikMessageBox.Show($"{ex2.Message}", MessageIcons.Error);
		}
		finally
		{
			ProgressBarVisibility = Visibility.Hidden;
		}
	}

	private async void ConfirmButton_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			ProgressBarVisibility = Visibility.Visible;
			string ip = ServerIp;
			uint port = ServerPort;
			if (ConfirmStackPanel.Visibility == Visibility.Visible)
			{
				if (string.IsNullOrEmpty(InputPasswordBox.Password))
				{
					PasswordTips = AppLocalizationService.GetLang("请输入密码");
					return;
				}
				if (string.IsNullOrEmpty(ConfirmPasswordBox.Password))
				{
					PasswordTips = AppLocalizationService.GetLang("Lang_PasswordConfirmTips");
					return;
				}
				if (InputPasswordBox.Password != ConfirmPasswordBox.Password)
				{
					PasswordTips = AppLocalizationService.GetLang("两次密码不一致");
					return;
				}
				if (InputPasswordBox.Password.Length < 3 || InputPasswordBox.Password.Length > 32)
				{
					PasswordTips = AppLocalizationService.GetLang("Lang_PasswordFailed");
					return;
				}
				Regex regular = new Regex("^[一-龥A-Za-z0-9_]+$");
				if (!regular.IsMatch(InputPasswordBox.Password))
				{
					HikMessageBox.Show("密码格式错误，密码不允许包含特殊字符！", MessageIcons.Warning);
					return;
				}
				PasswordTips = "";
				string password = InputPasswordBox.Password;
				int serverStatus = await Task.Run(() => ServerSDKAPI.SetServerPassword(ip.Trim(), port, password));
				LogHelper.Info($"ServerSDKAPI.SetServerPassword返回值：{$"0x{serverStatus:x2}"}");
				ProgressBarVisibility = Visibility.Hidden;
				if (serverStatus != 0)
				{
					HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(serverStatus), MessageIcons.Error);
					return;
				}
				SaveServerSetting($"{ServerIp}:{ServerPort}");
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PasswordModifiedSuccessfully"), MessageIcons.Ok);
			}
			else
			{
				string rsaPassword = RsaCryptHelper.GetInstance().Encryt(InputPasswordBox.Password);
				ConfigurationHelper.SaveConfig("ServerPassword", rsaPassword);
				uint status = await ReconnectServer($"{ip.Trim()}:{port}");
				ProgressBarVisibility = Visibility.Hidden;
				switch (status)
				{
				case 3758098438u:
					HikMessageBox.Show($"密码输入超过三次已锁定，请{waitingTime}分钟后尝试登录", MessageIcons.Error);
					return;
				default:
					HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo((int)status), MessageIcons.Error);
					ProgressBarVisibility = Visibility.Hidden;
					return;
				case 0u:
					break;
				}
				PasswordErrorCounter = 0;
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RemoteModeSetSuccessfully"), MessageIcons.Ok);
			}
			DialogResult = true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			HikMessageBox.Show(ex2.Message, MessageIcons.Error);
		}
		finally
		{
			NotifySolutionPathChanged?.Invoke();
			ProgressBarVisibility = Visibility.Hidden;
		}
	}

	private async void LocalMode_OnClick(object sender, RoutedEventArgs e)
	{
		try
		{
			ProgressBarVisibility = Visibility.Visible;
			uint status = await ReconnectServer("127.0.0.1:5556");
			ProgressBarVisibility = Visibility.Hidden;
			if (status != 0)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo((int)status), MessageIcons.Error);
				return;
			}
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LocalModeSetSuccessfully"), MessageIcons.Ok);
			DialogResult = false;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			HikMessageBox.Show(ex2.Message, MessageIcons.Error);
		}
		finally
		{
			ProgressBarVisibility = Visibility.Hidden;
		}
	}

	private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
	{
		if (sender is PasswordBox passwordBox)
		{
			PasswordBoxWaterMark.SetPassword(passwordBox, passwordBox.Password);
		}
	}

	private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		IsLegalIP = true;
	}
}
