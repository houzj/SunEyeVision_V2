using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Apps.ColorFun;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.ParamTab;
using Apps.XmlUI;
using Frontend.Base;
using FrontendUI.Design.Helper;
using Microsoft.Win32;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.User;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMBaseControls;
using VMControls.Data;
using VMControls.Interface;
using VisionMaster.AppMainWindow;
using VisionMaster.Project;
using VisionMasterMain;
using WindowsRecipes.TaskbarSingleInstance;
using WindowsRecipes.TaskbarSingleInstance.Wpf;

namespace VisionMaster;

internal static class Program
{
	public static int VisionMasterServerProcessID;

	private static string serverAddress = "";

	public static EncryptDefine.AUTH_LOCALHOST_LICENSE_TIME _stLicenseTime;

	private static bool _isFirstGetTime = true;

	private static DateTime _startTime;

	private static App app = null;

	private static string[] Arguments;

	public static MainWindowData MainData;

	private static BaseZmqCommand zmqCommand;

	[STAThread]
	private static void Main(string[] args)
	{
		Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
		string config = ConfigurationHelper.GetConfig("IsMultiApp");
		if (config == "1")
		{
			StartApp(args);
			return;
		}
		using (SingleInstanceManager.Initialize(GetSingleInstanceManagerSetup()))
		{
			StartApp(args);
		}
	}

	private static void StartApp(string[] args)
	{
		if (args.Length != 0)
		{
			RefreshRegistry();
		}
		Arguments = args;
		app = new App(Arguments);
		app.Run();
	}

	private static SingleInstanceManagerSetup GetSingleInstanceManagerSetup()
	{
		return new SingleInstanceManagerSetup(ConfigurationHelper.GetConfig("ApplicationName"))
		{
			ArgumentsHandler = (string[] args) =>
			{
				((App)Application.Current).ProcessCommandLineArgs(args);
			},
			ArgumentsHandlerInvoker = new ApplicationDispatcherInvoker()
		};
	}

	private static void RefreshRegistry()
	{
		RegistryKey classesRoot = Registry.ClassesRoot;
		RegistryKey registryKey = classesRoot.OpenSubKey("." + AppFileExtension.Solution, writable: true);
		if (registryKey != null)
		{
			registryKey.SetValue("", "vm310", RegistryValueKind.String);
			registryKey.Close();
		}
		classesRoot.Close();
	}

	public static void BeforeStartPart()
	{
		LogHelper.Info("---appBeforeStart--Beigin");
		SoftEncrypt();
		InitColor();
		EncryptAPI.TrialVersionInit();
		LogHelper.Info("SoftEncrypt");
		CPUConfig();
		LogHelper.Info("CPUConfig");
		CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
		if (!AccessManagerHelper.LoadAccessInfo())
		{
			LogHelper.objLog.Error("权限配置文件异常");
			UILogManager.logCreate("错误", AppLocalizationService.GetLang("权限配置文件异常"), false, true);
			throw new Exception("权限配置异常");
		}
		LogHelper.Info("GetSolutionTakeState");
		if (!VmSolution.GetSolutionTakeState())
		{
			LogHelper.Info("StartNewInstance");
			VmSolution.StartNewInstance();
		}
		Ioc.Set<IApplicationOperation>(new ApplicationOperation());
	}

	public static void BeforeStartMian()
	{
		InitMainWindow();
		LogHelper.Info("InitMainWindow--end");
	}

	public static void AppBeforStart()
	{
		LogHelper.Info("appBeforeStart--Beigin");
		EncryptAPI.TrialVersionInit();
		LogHelper.Info("Main Program Start");
		UILogManager.logCreate("信息", AppLocalizationService.GetLang("主界面程序启动"), false, true);
		LogHelper.Info("StartServerApp");
		LogHelper.Info("SoftEncrypt");
		SoftEncrypt();
		LogHelper.Info("InitMq");
		LogHelper.Info("CPUConfig");
		CPUConfig();
		LogHelper.Info("InitMainWindow");
		InitMainWindow();
		LogHelper.Info("appBeforeStart--end");
	}

	public static void InitMainWindow()
	{
		MainData = new MainWindowData();
		MainData.PreLoad();
		LogHelper.Info("InitServer");
		ServerManager.Instance.InitServer(TitleMenueItemData.GetToolFunctionSources);
		NameValueItem.logManager = (ILogManager)LogUIBridge.GetInstance();
	}

	public static void ReInitData()
	{
		ServerManager.Instance.InitServer(TitleMenueItemData.GetToolFunctionSources);
		LogHelper.Info("GetSolutionTakeState");
		if (!VmSolution.GetSolutionTakeState())
		{
			LogHelper.Info("StartNewInstance");
			VmSolution.StartNewInstance();
		}
	}

	internal static void AppAfterStart()
	{
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_0108: Invalid comparison between Unknown and I4
		LogHelper.Info("AppAfterStart-------begin");
		bool flag = false;
		if (Arguments != null && Arguments.Length != 0)
		{
			string text = Arguments[0];
			Arguments = null;
			if (!string.IsNullOrEmpty(text) && File.Exists(text) && Path.GetExtension(text) == "." + AppFileExtension.Solution)
			{
				flag = true;
				if (!SystemConfigManager.IsAutoLoad() && !MainData.SoftWareData.IsAutoLoadLast)
				{
					ProjectManager.GetInstance().LoadSolution(text, isGuid: false, null, null, isRemoteMode: false, isFromAppStart: true);
				}
				else
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Auto_load_case"), MessageIcons.Warning, MessageButtons.NoButton, 500u);
				}
			}
		}
		LogHelper.Info("AppAfterStart-------mid");
		AccessUser loginUser = CodeManager.GetInstance().GetLoginUser();
		if (!flag && GuideHelper.IsGuideWindowShow() && !SystemConfigManager.IsAutoLoad() && !IsSdkMode() && !((IVmSolution)VmSolution.Instance).IsTakeOver && (int)loginUser != 3)
		{
			LogHelper.Info("AppAfterStart-------GuideWindow---begin");
			GuideWindow win = null;
			object obj = WecPage(out win);
			LogHelper.Info("AppAfterStart-------GuideWindow---end");
			if (Application.Current.Dispatcher != ((DispatcherObject)(object)win).Dispatcher)
			{
				((DispatcherObject)(object)win).Dispatcher.InvokeShutdown();
			}
			LogHelper.Debug("GuideWindowShow close return value " + obj);
		}
		ToolBoxData.ChangePriorityByInfo();
		SoftWareData.SetAutoLaunchAccess();
		MainData.IsNormalMode = int.Parse(ConfigurationHelper.GetConfig("RunningMode")) != 1;
		string config = ConfigurationHelper.GetConfig("IsIgnoreGlobalModule");
		((IVmSolution)VmSolution.Instance).IsIgnoreGlobalModule = config == null || config == "1";
		SolutionFileHelper.GetInstance();
		((IVmSolution)VmSolution.Instance).IsClearLastExecuteResult = SystemConfigManager.Instance.IsClearLastExecuteResult;
		ImageViewBox.IsDirectDownSampling = SystemConfigManager.Instance.IsDirectDownSampling;
		VmSolution.IsCallBackCacheEnable = SystemConfigManager.Instance.IsCallBackCacheEnable;
		if (VmSolution.IsCallBackCacheEnable)
		{
			VmSolution.ExpandCallBackQueueLength(SystemConfigManager.Instance.CallBackQueueLength);
		}
		else
		{
			VmSolution.ShutDownCallBackQueue();
		}
		LogHelper.Info("AppAfterStart-------end");
	}

	internal static void ConfigZoomIndex()
	{
		MainData.ConfigZoomIndex = double.Parse(ConfigurationHelper.GetConfig("ZoomIndex"));
	}

	public static object WecPage()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		GuideWindow val = new GuideWindow();
		((FrameworkElement)(object)val).DataContext = ProjectManager.GetInstance();
		((FrameworkElement)(object)val).SetBinding(GuideWindow.RecentlySolutionsProperty, (BindingBase)new Binding("RecentlySolutions")
		{
			Mode = BindingMode.OneWay
		});
		((FrameworkElement)(object)val).SetBinding(GuideWindow.OpenSolutionCommandProperty, (BindingBase)new Binding("OpenSolutionCommand"));
		return ((BaseWindow)(object)val).ShowDialog();
	}

	public static object WecPage(out GuideWindow win)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		GuideWindow val = new GuideWindow();
		((FrameworkElement)(object)val).DataContext = ProjectManager.GetInstance();
		((FrameworkElement)(object)val).SetBinding(GuideWindow.RecentlySolutionsProperty, (BindingBase)new Binding("RecentlySolutions")
		{
			Mode = BindingMode.OneWay
		});
		((FrameworkElement)(object)val).SetBinding(GuideWindow.OpenSolutionCommandProperty, (BindingBase)new Binding("OpenSolutionCommand"));
		win = val;
		return ((BaseWindow)(object)val).ShowDialog();
	}

	internal static void InitLang()
	{
		string langParm = AppLocalizationService.GetLangParm();
		try
		{
			((ResourceDictionary)(object)LocalizationResourceDictionary.Instance).Source = new Uri("pack://SiteOfOrigin:,,,/Lang/" + langParm + ".xaml", UriKind.Absolute);
		}
		catch (Exception)
		{
		}
	}

	internal static void InitColor()
	{
		try
		{
			ColorThemesResourceDictionary.GetInstance();
		}
		catch (Exception)
		{
		}
	}

	public static bool IsCancelClose()
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Invalid comparison between Unknown and I4
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected Obj, but got Unknown
		if (IsSdkMode())
		{
			Application.Current.MainWindow.Visibility = Visibility.Collapsed;
			return true;
		}
		if ((int)CodeManager.GetInstance().GetLoginUser() == 3)
		{
			return false;
		}
		if (ConfigurationHelper.GetConfig("CloseSoftwarePasswordEnabled") == "1")
		{
			SoftWareData.LoadSoftwaraSet();
			AccesssCodeCheck val = new AccesssCodeCheck();
			((Window)(object)val).Title = AppLocalizationService.GetLang("验证关闭软件密码");
			bool bEnable = false;
			string code = "";
			SoftWareData.GetCloseSoftwareCode(out bEnable, out code);
			val.AccessAdministatorCode = code;
			if (((BaseWindow)(object)val).ShowDialog() == false)
			{
				return true;
			}
		}
		if (ConfigurationHelper.GetConfig("IsNotifyRunBackground") == "0")
		{
		}
		if (ConfigurationHelper.GetConfig("IsRunBackground") == "0" && SaveSolTip())
		{
			return true;
		}
		if (IsThirdSoftwareRun())
		{
			if (ConfigurationHelper.GetConfig("IsRunBackground") == "0")
			{
				HikMessageBox.Show("Lang_DevlopmentAndRunBackground", MessageIcons.None, isTranslate: true);
				return true;
			}
			HikMessageBox.Show("Lang_DevelopmentReloadSolution", MessageIcons.None, isTranslate: true);
		}
		return false;
	}

	public static void StartupVmFrontendWindowOnLinux()
	{
		MainData.IsShowProgressMask = false;
		if (!RemoteOperate.IsRemoteMode || ConfigurationHelper.GetConfig("IsRunBackground") == "1")
		{
			return;
		}
		string lang = AppLocalizationService.GetLang("ControlMessage");
		if (HikMessageBox.Show(lang, MessageButtons.YesOrNo, MessageIcons.Question) != DialogResult.Yes)
		{
			return;
		}
		string config = ConfigurationHelper.GetConfig("ServerSetting");
		if (config != null)
		{
			int num = ServerSDKAPI.StartupVmFrontendWindowOnLinux(config);
			LogHelper.Info("ServerSDKAPI.StartupVmFrontendWindowOnLinux返回值：" + $"0x{num:x2}");
			if (num != 0)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(num), MessageIcons.Error);
			}
		}
	}

	private static bool SaveSolTip()
	{
		bool flag = false;
		return ProjectManager.GetInstance().SolutionSaveCheck();
	}

	private static void CPUConfig()
	{
		try
		{
			string config = ConfigurationHelper.GetConfig("ProcessorAffinity");
			if (config != "0")
			{
				Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)Convert.ToInt32(config, 2);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_ProcessorAffinityFail", ex.Message, MessageIcons.Error);
		}
	}

	internal static void SoftEncrypt()
	{
		try
		{
			if (CodeManager.GetInstance().IsUseSoftEncrypt())
			{
				SystemConfigManager.Instance.IsSoftEncrypt = true;
				SystemConfigManager.Instance.StrEncryptType = AppLocalizationService.GetLang("软件期限");
				return;
			}
			SystemConfigManager.Instance.IsSoftEncrypt = false;
			if (RemoteOperate.IsRemoteMode)
			{
				SystemConfigManager.Instance.StrEncryptType = AppLocalizationService.GetLang("设备型号");
			}
			else
			{
				SystemConfigManager.Instance.StrEncryptType = AppLocalizationService.GetLang("加密狗");
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_StartErr", "Lang_CfgAbnormal", MessageIcons.Error, isTranslate: true);
			LogHelper.Info("启用软加密异常:" + ex.Message);
			Environment.Exit(0);
		}
	}

	public static bool IsNeedStartAuth(int ErrorCode)
	{
		if (ErrorCode == -536869115 || ErrorCode == -536869113)
		{
			return true;
		}
		return false;
	}

	public static bool IsNeedRemind()
	{
		if (SystemConfigManager.Instance.IsSoftEncrypt)
		{
			uint num = uint.Parse(ConfigurationHelper.GetConfig("OldAuthTime"));
			if (GetAuthTime() == 0 && _stLicenseTime.enLicenseType == EncryptDefine.AUTH_LOCALHOST_LICENSE_TYPE.AUTH_LOCALHOST_LICENSE_LIMIT_VALUE && num != _stLicenseTime.nAvailableDay)
			{
				ConfigurationHelper.SaveConfig("OldAuthTime", _stLicenseTime.nAvailableDay.ToString());
				if (_stLicenseTime.nAvailableDay <= 3)
				{
					return true;
				}
			}
		}
		return false;
	}

	private static int GetAuthTime()
	{
		if (_isFirstGetTime)
		{
			_isFirstGetTime = false;
			_startTime = DateTime.Now;
		}
		else
		{
			DateTime now = DateTime.Now;
			int num = (int)now.Subtract(_startTime).TotalSeconds;
			if (num < 300)
			{
				return 0;
			}
			_startTime = now;
		}
		_stLicenseTime = default;
		IntPtr intPtr = Marshal.AllocHGlobal(Marshal.SizeOf(_stLicenseTime));
		Marshal.StructureToPtr(_stLicenseTime, intPtr, fDeleteOld: false);
		int licenseTime = EncryptAPI.GetLicenseTime(EncryptDefine.AUTH_MACHINE_TYPE.AUTH_LOCAL_MACHINE, "", intPtr);
		if (licenseTime == 0)
		{
			_stLicenseTime = (EncryptDefine.AUTH_LOCALHOST_LICENSE_TIME)Marshal.PtrToStructure(intPtr, typeof(EncryptDefine.AUTH_LOCALHOST_LICENSE_TIME));
		}
		Marshal.FreeHGlobal(intPtr);
		return licenseTime;
	}

	public static void StartAuthActivation()
	{
		try
		{
			if ("zh-cn" == AppLocalizationService.GetLangType())
			{
				SetAuthToolLanguage(isChinese: true);
			}
			else
			{
				SetAuthToolLanguage(isChinese: false);
			}
			Process[] processesByName = Process.GetProcessesByName("SoftEncryptActive");
			if (processesByName.Length != 0 && processesByName[0].MainModule.FileName != AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "Tools\\AuthActiveTool\\SoftEncryptActive.exe")
			{
				processesByName[0].Kill();
			}
			Process process = new Process();
			process.StartInfo.FileName = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "Tools\\AuthActiveTool\\SoftEncryptActive.exe";
			process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "Tools\\AuthActiveTool\\";
			process.Start();
			process.WaitForExit();
			process.Close();
		}
		catch (Exception ex)
		{
			LogHelper.Error("授权激活进程启动失败 :" + ex.Message);
		}
	}

	public static void SetAuthToolLanguage(bool isChinese)
	{
		string path = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "Tools\\AuthActiveTool\\cfg\\LocalConfig.ini";
		if (!File.Exists(path))
		{
			return;
		}
		string[] array = File.ReadAllLines(path);
		for (int i = 0; i < array.Length; i++)
		{
			if (array[i].Contains("Language"))
			{
				if (isChinese)
				{
					array[i] = "Language=1";
				}
				else
				{
					array[i] = "Language=0";
				}
				break;
			}
		}
		File.WriteAllLines(path, array);
	}

	private static void AwakeServerAsProcess()
	{
		try
		{
			string text = "VisionMasterServer.exe";
			Process[] processesByName = Process.GetProcessesByName(text);
			if (processesByName.Length != 0)
			{
				FileInfo fileInfo = new FileInfo(processesByName[0].MainModule.FileName);
				FileInfo fileInfo2 = new FileInfo(Process.GetCurrentProcess().MainModule.FileName);
				if (fileInfo.DirectoryName.Contains(fileInfo2.DirectoryName))
				{
					VisionMasterServerProcessID = processesByName[0].Id;
					return;
				}
				processesByName[0].Kill();
				processesByName[0].WaitForExit(5000);
			}
			ProcessStartInfo processStartInfo = new ProcessStartInfo("Server\\" + text, "-d");
			processStartInfo.CreateNoWindow = true;
			processStartInfo.UseShellExecute = false;
			processStartInfo.RedirectStandardOutput = true;
			Process process = Process.Start(processStartInfo);
			VisionMasterServerProcessID = process.Id;
		}
		catch (Exception ex)
		{
			LogHelper.Debug("启动服务asexe失败:" + ex.Message);
			Environment.Exit(0);
		}
	}

	private static void AwakeServer()
	{
		try
		{
			string serviceName = "VisionMasterServer";
			LogHelper.Debug("判断服务是否存在");
			if (!ServiceManager.ServiceIsExisted(serviceName))
			{
				LogHelper.Debug("判断服务不存在，安装服务");
				ServiceManager.InstallService();
			}
			else if (!ServiceManager.CheckServiceDirectory(serviceName))
			{
				LogHelper.Debug("路径不一致重装服务");
				ServiceManager.InstallService();
			}
			for (int i = 0; i < 20; i++)
			{
				Thread.Sleep(50);
				if (ServiceManager.ServiceIsExisted(serviceName))
				{
					LogHelper.Debug("服务安装完毕");
					break;
				}
			}
			LogHelper.Debug("开始启动服务");
			if (!ServiceManager.ServiceIsRunning(serviceName))
			{
				LogHelper.Debug("接口启动服务");
				ServiceManager.StartService(serviceName);
			}
			LogHelper.Debug("服务操作完毕");
		}
		catch (Exception ex)
		{
			LogHelper.Debug("启动服务失败");
			throw ex;
		}
	}

	internal static void StartServerApp()
	{
		Task.Run(() =>
		{
			try
			{
				Process[] processesByName = Process.GetProcessesByName("VisionMasterServerApp");
				if (processesByName.Length != 0 && processesByName[0].MainModule.FileName != AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "VisionMasterServerApp\\VisionMasterServerApp.exe")
				{
					processesByName[0].Kill();
				}
				Process process = new Process();
				process.StartInfo.FileName = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "VisionMasterServerApp\\VisionMasterServerApp.exe";
				process.StartInfo.WorkingDirectory = AppDomain.CurrentDomain.SetupInformation.ApplicationBase + "VisionMasterServerApp\\";
				process.Start();
			}
			catch (Exception ex)
			{
				LogHelper.Error("服务应用启动失败 :" + ex.Message);
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ServerAppStartFail"), ex.Message, MessageIcons.Error);
			}
		});
	}

	private static int InitMq()
	{
		zmqCommand = new HkrMQCommand();
		if (!zmqCommand.connectSuccess)
		{
			HikMessageBox.Show("Lang_StartErr", (string)Application.Current.TryFindResource("Lang_SdkCommError"), MessageIcons.Error);
			return -1;
		}
		UILogManager.logCreate("信息", AppLocalizationService.GetLang("服务sdk初始化操作"), false, true);
		return 0;
	}

	public static bool IsSdkMode()
	{
		if (Arguments != null && Arguments.Length != 0 && Arguments[0].Contains("tcp://"))
		{
			return true;
		}
		return false;
	}

	public static bool IsThirdSoftwareRun()
	{
		try
		{
			return ServerSDKManager.serverSDKManager.mSolutionManager.bIsThirdSoftwareRun();
		}
		catch
		{
			return false;
		}
	}

	internal static void CheckSecondaryDevelopmentProcessStarted()
	{
		if (IsThirdSoftwareRun())
		{
			Application.Current.Dispatcher.Invoke(() =>
			{
				HikMessageBox.Show("Lang_SecondaryDevelopmentProcessStarted", MessageIcons.Warning, isTranslate: true);
			});
		}
	}

	internal static void UpdateTitleBinding()
	{
		if (MainData.MenuToolSource is List<TitleMenueItemData> source)
		{
			IEnumerable<TitleMenueItemData> enumerable = source.Where((TitleMenueItemData x) => x.getCustomItemDataContextMethodName == "GetUnRedoStack");
			foreach (TitleMenueItemData item in enumerable)
			{
				object customItemData = item.CustomItemData;
			}
		}
		SoftWareData.StartSetParamModify();
		SoftWareData.StartSetParamAboutRender();
		SoftWareData.EnableWatchDog();
	}
}
