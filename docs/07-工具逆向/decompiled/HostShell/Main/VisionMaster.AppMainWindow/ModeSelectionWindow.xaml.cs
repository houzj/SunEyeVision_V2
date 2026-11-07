using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Hik.Util.Security;
using VM.Controls;
using VM.PlatformSDKCS;
using VM.Utility;
using VisionMasterMain.AppMainWindow;

namespace VisionMaster.AppMainWindow;

public partial class ModeSelectionWindow : BaseWindow, IComponentConnector
{
	public static readonly DependencyProperty IsRememberCheckedProperty = DependencyProperty.Register("IsRememberChecked", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(false, IsRememberCheckedPropertyChangedCallback));

	public static readonly DependencyProperty ServerIpProperty = DependencyProperty.Register("ServerIp", typeof(string), typeof(ModeSelectionWindow), new PropertyMetadata("127.0.0.1", ServerIpPropertyChangedCallback));

	public static readonly DependencyProperty ServerPortProperty = DependencyProperty.Register("ServerPort", typeof(uint), typeof(ModeSelectionWindow), new PropertyMetadata(5556u, ServerIpOrPortPropertyChangedCallback));

	public static readonly DependencyProperty IsRemoteModeProperty = DependencyProperty.Register("IsRemoteMode", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(true, IsRemoteModePropertyChangedCallback));

	public static readonly DependencyProperty ProgressBarVisibilityProperty = DependencyProperty.Register("ProgressBarVisibility", typeof(Visibility), typeof(ModeSelectionWindow), new PropertyMetadata(Visibility.Hidden, ProgressBarVisibilityPropertyChangedCallback));

	public static readonly DependencyProperty PasswordTipsProperty = DependencyProperty.Register("PasswordTips", typeof(string), typeof(ModeSelectionWindow), new PropertyMetadata(""));

	public static readonly DependencyProperty ConfirmPasswordTipsProperty = DependencyProperty.Register("ConfirmPasswordTips", typeof(string), typeof(ModeSelectionWindow), new PropertyMetadata(""));

	public static readonly DependencyProperty IsNeedInputPasswordBoxPswProperty = DependencyProperty.Register("IsNeedInputPasswordBox", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(false));

	public static readonly DependencyProperty IsNeedConfirmPasswordBoxProperty = DependencyProperty.Register("IsNeedConfirmPasswordBox", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(false));

	public static readonly DependencyProperty ServersProperty = DependencyProperty.Register("Servers", typeof(ObservableCollection<string>), typeof(ModeSelectionWindow), new PropertyMetadata(null));

	public static readonly DependencyProperty IsLegalIPProperty = DependencyProperty.Register("IsLegalIP", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(true));

	public static readonly DependencyProperty IsOpenNativeModeProperty = DependencyProperty.Register("IsOpenNativeMode", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(false));

	public static readonly DependencyProperty IsEnumIpProperty = DependencyProperty.Register("IsEnumIp", typeof(bool), typeof(ModeSelectionWindow), new PropertyMetadata(false));

	public static readonly DependencyProperty CountDownTimeProperty = DependencyProperty.Register("CountDownTime", typeof(string), typeof(ModeSelectionWindow), new PropertyMetadata(null, CountDownTimePropertyChangedCallback));

	private static List<DeviceInfo> EnumIpList { get; set; }

	private DispatcherTimer CountDownTimer { get; set; }

	public bool IsRememberChecked
	{
		get
		{
			return (bool)GetValue(IsRememberCheckedProperty);
		}
		set
		{
			SetValue(IsRememberCheckedProperty, value);
		}
	}

	public ObservableCollection<string> Servers
	{
		get
		{
			return (ObservableCollection<string>)GetValue(ServersProperty);
		}
		set
		{
			SetValue(ServersProperty, value);
		}
	}

	public string ServerIp
	{
		get
		{
			return (string)GetValue(ServerIpProperty);
		}
		set
		{
			SetValue(ServerIpProperty, value);
		}
	}

	public uint ServerPort
	{
		get
		{
			return (uint)GetValue(ServerPortProperty);
		}
		set
		{
			SetValue(ServerPortProperty, value);
		}
	}

	public bool IsRemoteMode
	{
		get
		{
			return (bool)GetValue(IsRemoteModeProperty);
		}
		set
		{
			SetValue(IsRemoteModeProperty, value);
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

	public string PasswordTips
	{
		get
		{
			return (string)GetValue(PasswordTipsProperty);
		}
		set
		{
			SetValue(PasswordTipsProperty, value);
		}
	}

	public string ConfirmPasswordTips
	{
		get
		{
			return (string)GetValue(ConfirmPasswordTipsProperty);
		}
		set
		{
			SetValue(ConfirmPasswordTipsProperty, value);
		}
	}

	public string CountDownTime
	{
		get
		{
			return (string)GetValue(CountDownTimeProperty);
		}
		set
		{
			SetValue(CountDownTimeProperty, value);
		}
	}

	public bool IsNeedInputPasswordBox
	{
		get
		{
			return (bool)GetValue(IsNeedInputPasswordBoxPswProperty);
		}
		set
		{
			SetValue(IsNeedInputPasswordBoxPswProperty, value);
		}
	}

	public bool IsNeedConfirmPasswordBox
	{
		get
		{
			return (bool)GetValue(IsNeedConfirmPasswordBoxProperty);
		}
		set
		{
			SetValue(IsNeedConfirmPasswordBoxProperty, value);
		}
	}

	public bool IsLegalIP
	{
		get
		{
			return (bool)GetValue(IsLegalIPProperty);
		}
		set
		{
			SetValue(IsLegalIPProperty, value);
		}
	}

	public bool IsOpenNativeMode
	{
		get
		{
			return (bool)GetValue(IsOpenNativeModeProperty);
		}
		set
		{
			SetValue(IsOpenNativeModeProperty, value);
		}
	}

	public bool IsEnumIp
	{
		get
		{
			return (bool)GetValue(IsEnumIpProperty);
		}
		set
		{
			SetValue(IsEnumIpProperty, value);
		}
	}

	private uint CountDownSeconds { get; set; }

	private int TimeCounter { get; set; }

	private static void ServerIpPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ServerIpOrPortPropertyChangedCallback(d, e);
		if (d is ModeSelectionWindow modeSelectionWindow && e.NewValue != null && e.NewValue?.ToString() != e.OldValue?.ToString())
		{
			List<DeviceInfo> enumIpList = EnumIpList;
			modeSelectionWindow.IsEnumIp = enumIpList != null && enumIpList.Where((DeviceInfo dev) => dev.CurrentIP == e.NewValue.ToString()).Count() > 0;
			modeSelectionWindow.CountDownTimer?.Stop();
			modeSelectionWindow.CountDownTime = null;
		}
	}

	private static void ServerIpOrPortPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ModeSelectionWindow modeSelectionWindow && e.NewValue != null && e.NewValue?.ToString() != e.OldValue?.ToString())
		{
			modeSelectionWindow.IsLegalIP = true;
			modeSelectionWindow.ConfirmButton.IsEnabled = false;
			modeSelectionWindow.CheckButton.Visibility = Visibility.Visible;
			modeSelectionWindow.PasswordTips = null;
			modeSelectionWindow.ConfirmPasswordTips = null;
			modeSelectionWindow.IsNeedInputPasswordBox = false;
			modeSelectionWindow.IsNeedConfirmPasswordBox = false;
			modeSelectionWindow.InputPasswordBox.Password = "";
			modeSelectionWindow.ConfirmPasswordBox.Password = "";
			modeSelectionWindow.CountDownTimer?.Stop();
			modeSelectionWindow.CountDownTime = null;
		}
	}

	private static void IsRememberCheckedPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (e.NewValue is bool flag && e.OldValue != null)
		{
			ConfigurationHelper.SaveConfig("IsRemeberServerChecked", flag.ToString());
		}
	}

	private static void IsRemoteModePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ModeSelectionWindow modeSelectionWindow && e.NewValue is bool flag)
		{
			modeSelectionWindow.ConfirmButton.IsEnabled = !flag;
		}
	}

	private static void CountDownTimePropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ModeSelectionWindow modeSelectionWindow)
		{
			if (string.IsNullOrEmpty(e.NewValue?.ToString()))
			{
				modeSelectionWindow.CheckButton.IsEnabled = true;
				modeSelectionWindow.ConfirmButton.IsEnabled = true;
			}
			else
			{
				modeSelectionWindow.CheckButton.IsEnabled = false;
				modeSelectionWindow.ConfirmButton.IsEnabled = false;
			}
		}
	}

	private static void ProgressBarVisibilityPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is ModeSelectionWindow modeSelectionWindow && e.NewValue is Visibility visibility)
		{
			if (visibility == Visibility.Visible)
			{
				modeSelectionWindow.CheckButton.IsEnabled = false;
				modeSelectionWindow.ConfirmButton.IsEnabled = false;
			}
			else
			{
				modeSelectionWindow.CheckButton.IsEnabled = true;
				modeSelectionWindow.ConfirmButton.IsEnabled = true;
			}
		}
	}

	public ModeSelectionWindow()
	{
		InitializeComponent();
		Loaded += ModeSelectionWindow_Loaded;
	}

	private List<string> GetIpList(out string ip, out uint port)
	{
		ip = "127.0.0.1";
		port = 5556u;
		List<string> list = new List<string>();
		EnumIpList = DeviceUtil.GetDeviceList();
		list = EnumIpList.Select((DeviceInfo d) => d.CurrentIP).ToList();
		string config = ConfigurationHelper.GetConfig("ServerSetting");
		if (config != null)
		{
			string[] array = config.Split(':');
			if (array.Length == 2)
			{
				ip = array[0];
				if (uint.TryParse(array[1], out port))
				{
					ServerPort = port;
				}
			}
		}
		return list;
	}

	private void StartCountDownTimer(uint countDownSeconds)
	{
		string lang = AppLocalizationService.GetLang("PasswordMessageBoxTips");
		CountDownTime = string.Format(lang, countDownSeconds);
		CountDownTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(60.0)
		};
		CountDownTimer.Tick -= CountDownTimer_Tick;
		CountDownTimer.Tick += CountDownTimer_Tick;
		CountDownSeconds = countDownSeconds;
		CountDownTimer.Start();
	}

	private async void CountDownTimer_Tick(object sender, EventArgs e)
	{
		long currentTime = CountDownSeconds - ++TimeCounter;
		if (currentTime > 0)
		{
			string messageTips = AppLocalizationService.GetLang("PasswordMessageBoxTips");
			CountDownTime = string.Format(messageTips, currentTime);
			return;
		}
		TimeCounter = 0;
		CountDownTimer.Stop();
		string ip = ServerIp;
		uint port = ServerPort;
		uint time = 0u;
		await Task.Run(() =>
		{
			ServerSDKAPI.CheckServerPasswordStatus(ip.Trim(), port, ref time);
		});
		if (time != 0)
		{
			StartCountDownTimer(time);
		}
		else
		{
			CountDownTime = null;
		}
	}

	private void IpComboBox_DropDownOpened(object sender, EventArgs e)
	{
		Servers = new ObservableCollection<string>(GetIpList(out var _, out var _));
	}

	private void ModeSelectionWindow_Loaded(object sender, RoutedEventArgs e)
	{
		Uri uriSource = new Uri($"{AppDomain.CurrentDomain.BaseDirectory}Resources\\guide_logo.png", UriKind.RelativeOrAbsolute);
		titleLogo.Source = new BitmapImage(uriSource);
		Servers = new ObservableCollection<string>(GetIpList(out var ip, out var port));
		Servers = new ObservableCollection<string>(EnumIpList.Select((DeviceInfo d) => d.CurrentIP).ToList());
		ServerIp = ip;
		ServerPort = port;
		IsRemoteMode = ServerIp != "127.0.0.1";
		localRadio.IsChecked = !IsRemoteMode;
		if (bool.TryParse(ConfigurationHelper.GetConfig("IsOpenNativeMode"), out var result))
		{
			IsOpenNativeMode = result;
			if (!IsOpenNativeMode)
			{
				IsRemoteMode = true;
			}
		}
	}

	private void Close_OnClick(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}

	private async void CheckButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			if (IsRemoteMode)
			{
				IsLegalIP = IPAddress.TryParse(ServerIp, out var _);
				if (!IsLegalIP)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("IPLegalTips"), MessageIcons.Warning);
					return;
				}
			}
			ProgressBarVisibility = Visibility.Visible;
			string ip = ServerIp;
			uint port = ServerPort;
			uint countDownTimer = 0u;
			uint serverStatus = await Task.Run(() => (uint)ServerSDKAPI.CheckServerPasswordStatus(ip.Trim(), port, ref countDownTimer));
			LogHelper.Info($"ServerSDKAPI.CheckServerPasswordStatus返回值：{$"0x{serverStatus:x2}"}");
			ProgressBarVisibility = Visibility.Hidden;
			switch (serverStatus)
			{
			case 3758098434u:
				ConfirmButton.IsEnabled = true;
				CheckButton.Visibility = Visibility.Collapsed;
				InputTextBlock.Text = AppLocalizationService.GetLang("Lang_SetScriptPassword");
				IsNeedInputPasswordBox = true;
				IsNeedConfirmPasswordBox = true;
				PasswordTips = AppLocalizationService.GetLang("请输入密码");
				break;
			case 3758098437u:
				ConfirmButton.IsEnabled = true;
				CheckButton.Visibility = Visibility.Collapsed;
				InputTextBlock.Text = AppLocalizationService.GetLang("输入密码");
				IsNeedInputPasswordBox = true;
				IsNeedConfirmPasswordBox = false;
				PasswordTips = AppLocalizationService.GetLang("请输入密码");
				break;
			case 3758098438u:
				StartCountDownTimer(countDownTimer);
				break;
			case 0u:
				if (ServerIp == "127.0.0.1")
				{
					uint errorCode = 3758096643u;
					HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo((int)errorCode), MessageIcons.Error);
				}
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
			LogHelper.Info($"控制器检查失败：{ex2.Message},{ex2.StackTrace?.ToString()}");
			HikMessageBox.Show($"{ex2.Message}", MessageIcons.Error);
		}
	}

	private async void Confirm_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			string ip = ServerIp;
			uint port = ServerPort;
			string password = InputPasswordBox.Password;
			string serverSettingConnection = null;
			if (IsRemoteMode)
			{
				ConfirmPasswordTips = "";
				serverSettingConnection = $"{ip.Trim()}:{port}";
				string rsaPassword = RsaCryptHelper.GetInstance().Encryt(password);
				ConfigurationHelper.SaveConfig("ServerPassword", rsaPassword);
			}
			else
			{
				serverSettingConnection = "127.0.0.1:5556";
			}
			if (IsNeedConfirmPasswordBox)
			{
				if (InputPasswordBox.Password != ConfirmPasswordBox.Password)
				{
					ConfirmPasswordTips = AppLocalizationService.GetLang("两次密码不一致");
					return;
				}
				Regex regular = new Regex("^(?![0-9]+$)(?![a-zA-Z]+$)[0-9A-Za-z]{8,16}$");
				if (!regular.IsMatch(InputPasswordBox.Password))
				{
					ProgressBarVisibility = Visibility.Hidden;
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PasswordFailed"), MessageIcons.Warning);
					return;
				}
				ProgressBarVisibility = Visibility.Visible;
				int serverStatus = await Task.Run(() => ServerSDKAPI.SetServerPassword(ip.Trim(), port, password));
				LogHelper.Info($"ServerSDKAPI.SetServerPassword返回值：{$"0x{serverStatus:x2}"}");
				ProgressBarVisibility = Visibility.Hidden;
				if (serverStatus != 0)
				{
					HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(serverStatus), MessageIcons.Error);
					return;
				}
			}
			ProgressBarVisibility = Visibility.Visible;
			int waitingTime = 0;
			uint reconnectServerStatus = await Task.Run(() => CVmObjectBase.ReconnectServer(serverSettingConnection, ref waitingTime));
			LogHelper.Info($"CVmObjectBase.ReconnectServer返回值：{$"0x{reconnectServerStatus:x2}"}");
			ProgressBarVisibility = Visibility.Hidden;
			switch (reconnectServerStatus)
			{
			case 3758098438u:
				StartCountDownTimer((uint)waitingTime);
				break;
			default:
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo((int)reconnectServerStatus), MessageIcons.Error);
				break;
			case 0u:
				ConfigurationHelper.SaveConfig("ServerSetting", serverSettingConnection);
				DialogResult = true;
				break;
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			LogHelper.Info($"控制器连接失败：{ex2.Message},{ex2.StackTrace?.ToString()}");
			HikMessageBox.Show($"{ex2.Message}", MessageIcons.Error);
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
		PasswordTips = (string.IsNullOrEmpty(InputPasswordBox.Password) ? AppLocalizationService.GetLang("请输入密码") : "");
	}

	private async void ModifyIpButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			DeviceInfo deviceInfo = EnumIpList.Where((DeviceInfo d) => d.CurrentIP == ServerIp).FirstOrDefault();
			ModifyIpWindow modifyIpWindow = new ModifyIpWindow((DeviceInfo)deviceInfo.Clone(), EnumIpList.Select((DeviceInfo t) => t.CurrentIP));
			if (modifyIpWindow.ShowDialog() != true)
			{
				return;
			}
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			ProgressBarVisibility = Visibility.Visible;
			string serialNumber = modifyIpWindow.CurrentDevice.SerialNumber;
			bool isQuerySuccess = false;
			await Task.Run(() =>
			{
				while (stopwatch.ElapsedMilliseconds <= 60000)
				{
					EnumIpList = DeviceUtil.GetDeviceList();
					List<DeviceInfo> enumIpList = EnumIpList;
					if (enumIpList == null || enumIpList.Count <= 0)
					{
						IsEnumIp = false;
						ServerIp = null;
						break;
					}
					ObservableCollection<string> servers = new ObservableCollection<string>(EnumIpList.Select((DeviceInfo d) => d.CurrentIP).ToList());
					DeviceInfo deviceInfo2 = EnumIpList.Where((DeviceInfo s) => s.SerialNumber == serialNumber)?.FirstOrDefault();
					DeviceInfo device;
					if (deviceInfo2 != null && (device = deviceInfo2) != null)
					{
						stopwatch.Stop();
						Dispatcher?.Invoke(() =>
						{
							isQuerySuccess = true;
							Servers = servers;
							ServerIp = device.CurrentIP;
							ProgressBarVisibility = Visibility.Hidden;
						});
						break;
					}
					Thread.Sleep(3000);
				}
			});
			stopwatch.Stop();
			ProgressBarVisibility = Visibility.Hidden;
			if (!isQuerySuccess)
			{
				HikMessageBox.Show($"查询超时，找不到原IP为{deviceInfo.CurrentIP}的设备！请检查对应的设备！", MessageIcons.Warning);
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			ProgressBarVisibility = Visibility.Hidden;
			LogHelper.Error($"IP修改失败：{ex2.Message},{ex2.StackTrace?.ToString()}");
			HikMessageBox.Show($"{ex2.Message}", MessageIcons.Error);
		}
	}
}
