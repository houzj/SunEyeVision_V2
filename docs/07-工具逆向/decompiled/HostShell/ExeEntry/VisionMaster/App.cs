using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlUI;
using Apps.XmlUI.ParamTabControl;
using FrontendUI.Design;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.User;
using VM.Utility;
using VMControls.Interface;
using VisionMaster.AppMainWindow;
using VisionMaster.Project;
using VisionMasterMain;

namespace VisionMaster;

public class App : Application
{
	public string[] args;

	private static DispatcherTimer dispatcherTimer;

	private AutoResetEvent mWaitEvent = new AutoResetEvent(initialState: false);

	private AutoResetEvent mWaitEventBefore = new AutoResetEvent(initialState: false);

	private Exception _subThreadException = null;

	public App()
	{
		CultureInfo currentCulture = CultureInfo.CurrentCulture;
		LogHelper.Info("CurrentSystemLanguage: " + currentCulture.Name);
		if (!currentCulture.Name.StartsWith("zh"))
		{
			CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("en-US");
			LogHelper.Info("The program's global culture environment is set to: " + Thread.CurrentThread.CurrentCulture.Name);
		}
		DispatcherUnhandledException += App_DispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
		TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
		EnableTimerGC();
	}

	public static void EnableTimerGC()
	{
		try
		{
			if (Convert.ToBoolean(ConfigurationHelper.GetConfig("IsStartTimerGC")))
			{
				dispatcherTimer = new DispatcherTimer
				{
					Interval = new TimeSpan(0, 0, 0, 5, 0)
				};
				dispatcherTimer.Tick += DispatcherTimer_Tick;
				dispatcherTimer.Start();
			}
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
		}
	}

	private static void DispatcherTimer_Tick(object sender, EventArgs e)
	{
		GC.Collect();
	}

	public App(string[] args)
		: this()
	{
		this.args = args;
	}

	protected override void OnStartup(StartupEventArgs e)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Expected Obj, but got Unknown
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_014f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0151: Invalid comparison between Unknown and I4
		if (Convert.ToBoolean(ConfigurationHelper.GetConfig("ShowEnable")))
		{
			WelcomeDialog animatedSplashScreenWindow = null;
			Thread thread = new Thread(() =>
			{
				//IL_000d: Unknown result type (might be due to invalid IL or missing references)
				//IL_0017: Expected Obj, but got Unknown
				LogHelper.Info("WelcomeDialog----begin");
				animatedSplashScreenWindow = new WelcomeDialog();
				((Window)(object)animatedSplashScreenWindow).Show();
				mWaitEvent.WaitOne();
				((DispatcherObject)(object)animatedSplashScreenWindow).Dispatcher.InvokeShutdown();
				animatedSplashScreenWindow = null;
			})
			{
				IsBackground = true
			};
			thread.SetApartmentState(ApartmentState.STA);
			thread.Start();
		}
		PreLoadPage val = null;
		Thread thread2 = new Thread(() =>
		{
			try
			{
				LogHelper.Info("BeforeStartPart-----begin");
				Program.BeforeStartPart();
				LogHelper.Info("BeforeStartPart-----end");
			}
			catch (Exception subThreadException)
			{
				_subThreadException = subThreadException;
			}
			finally
			{
				mWaitEventBefore.Set();
			}
		})
		{
			IsBackground = true,
			Priority = ThreadPriority.Highest
		};
		thread2.SetApartmentState(ApartmentState.STA);
		thread2.Start();
		try
		{
			LogHelper.Info("Main-----begin");
			Program.BeforeStartMian();
			LogHelper.Info("PreLoadPage-----begin");
			val = new PreLoadPage();
			LogHelper.Info("PreLoadPage-----end");
			LogHelper.Info("初始化完成");
		}
		catch (Exception ex)
		{
			DoneException(ex);
		}
		mWaitEventBefore.WaitOne();
		LogHelper.Info("初始化完成---线程前---waitone后");
		if (_subThreadException != null)
		{
			DoneException(_subThreadException);
		}
		mWaitEvent.Set();
		LogHelper.Info("thread----start后---AppAfterStart前");
		Program.AppAfterStart();
		LogHelper.Info("thread----start后---AppAfterStart后");
		AccessUser loginUser = CodeManager.GetInstance().GetLoginUser();
		LogHelper.Info("GetLoginUser后");
		if ((int)loginUser != 3)
		{
			if (Convert.ToBoolean(ConfigurationHelper.GetConfig("FrontendFirst")))
			{
				ShowFrontendUI();
			}
			else
			{
				LogHelper.Info("ShowMainWindow------begin");
				ShowMainWindow();
				LogHelper.Info("ShowMainWindow------end");
			}
		}
		else
		{
			ShowFrontendUI();
		}
		LogHelper.Info("MainWindow----display---end");
		if (ProjectManager.OpenRecentSolutionInfo != null)
		{
			if (!ProjectManager.OpenRecentSolution(ProjectManager.OpenRecentSolutionInfo, isGuid: true) && HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolOpenErr"), AppLocalizationService.GetLang("Lang_SolOpenErrtoRM"), MessageButtons.YesOrNo, MessageIcons.Error, isTranslate: false, isShowInThread: true) == DialogResult.Yes)
			{
				ProjectManager.RemoveRecentSolution(ProjectManager.OpenRecentSolutionInfo.SolutionPath);
			}
			ProjectManager.OpenRecentSolutionInfo = null;
		}
		Thread thread3 = new Thread(() =>
		{
			if (Program.IsNeedRemind())
			{
				string strMsg = AppLocalizationService.GetLang("Lang_SoftAuthRemindStart") + Convert.ToString(Program._stLicenseTime.nAvailableDay) + AppLocalizationService.GetLang("Lang_SoftAuthRemindEnd");
				DialogResult dialogResult = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Award"), strMsg, MessageButtons.YesOrNo, MessageIcons.Warning);
				if (dialogResult == DialogResult.Yes)
				{
					Program.StartAuthActivation();
				}
			}
		});
		thread3.SetApartmentState(ApartmentState.STA);
		thread3.IsBackground = true;
		thread3.Start();
		string text = AppDomain.CurrentDomain.BaseDirectory + ConfigurationHelper.GetConfig("UnRedoServiceImplement");
		LogHelper.Info("撤销重做注入ioc,准备的dll路径为" + text);
		Assembly assembly = Assembly.LoadFrom(text);
		Type typeto = assembly.GetTypes().FirstOrDefault((Type c) => c.GetInterfaces().Contains(typeof(IUnRedoService)));
		Ioc.Set(typeof(IUnRedoService), typeto);
		Program.UpdateTitleBinding();
		bool flag = Convert.ToBoolean(ConfigurationHelper.GetConfig("IsMaximizedForVM"));
		bool flag2 = Convert.ToBoolean(ConfigurationHelper.GetConfig("IsFullScreenForVM"));
		if (flag && Application.Current.MainWindow.WindowState != WindowState.Maximized)
		{
			Application.Current.MainWindow.WindowState = WindowState.Maximized;
		}
		if (flag2)
		{
			Application.Current.MainWindow.Left = 0.0;
			Application.Current.MainWindow.Top = 0.0;
			Application.Current.MainWindow.Width = SystemParameters.PrimaryScreenWidth;
			Application.Current.MainWindow.Height = SystemParameters.PrimaryScreenHeight;
		}
	}

	private static void ShowFrontendUI()
	{
		Window window = FrontendWindowData.Instance.InitRunWindowFromDesignData();
		if (window != null)
		{
			Application.Current.MainWindow = window;
			window.ShowInTaskbar = true;
			window.Show();
			Ioc.Get<IApplicationOperation>()?.RegisterMainWindow(window);
		}
	}

	private void DeEventWait()
	{
		mWaitEvent.Dispose();
		mWaitEventBefore.Dispose();
	}

	private void DoneException(Exception ex)
	{
		VmException vmException = GetVmException(ex);
		if (vmException != null)
		{
			LogHelper.objLog.Error("启动失败:" + vmException.errorMessage);
			if (Program.IsNeedStartAuth(vmException.errorCode))
			{
				string text = "";
				DialogResult dialogResult = HikMessageBox.Show(strMsg: (vmException.errorCode != -536869115) ? AppLocalizationService.GetLang("软件授权已过期，是否进行重新授权？") : AppLocalizationService.GetLang("软件未激活，是否进行授权激活？"), title: AppLocalizationService.GetLang("Lang_Award"), messageButtons: MessageButtons.YesOrNo, icons: MessageIcons.Warning);
				if (dialogResult == DialogResult.Yes)
				{
					Program.StartAuthActivation();
					Thread.Sleep(5000);
					try
					{
						Program.ReInitData();
						return;
					}
					catch (Exception ex2)
					{
						VmException vmException2 = GetVmException(ex2);
						if (vmException2 != null)
						{
							LogHelper.objLog.Error("重新启动失败:" + vmException2.errorMessage);
						}
						else
						{
							LogHelper.objLog.Error("重新启动失败:" + ex2.ToString());
						}
						DeInitExit();
						return;
					}
				}
				DeInitExit();
				return;
			}
			if (vmException.errorCode == -536869093)
			{
				string strMsg = ErrorCodeInfo.GetErrorInfo(vmException.errorCode) + AppLocalizationService.GetLang("逗号") + AppLocalizationService.GetLang("是否打开帮助文档");
				DialogResult dialogResult2 = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Award"), strMsg, MessageButtons.YesOrNo, MessageIcons.Warning);
				if (dialogResult2 == DialogResult.Yes)
				{
					GuideHelper.GetRemoteDongleHelp();
				}
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_StartErr"), ErrorCodeInfo.GetErrorInfo(vmException.errorCode), MessageIcons.Error);
			}
			DeInitExit();
		}
		else
		{
			LogHelper.objLog.Error("启动失败:" + ex.ToString());
			if (ex.Message.Contains("VM.Core.TempVmSolution"))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_StartErr"), AppLocalizationService.GetLang("Lang_EnvironmentAbnormalities"), MessageIcons.Error);
				OpenEnvironmentDetectionToolExe("Tools\\EnvironmentDetectionTool\\EnvironmentDetectionTool.exe");
			}
			else
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_StartErr"), ex.Message, MessageIcons.Error);
			}
			DeInitExit();
		}
	}

	private void OpenEnvironmentDetectionToolExe(string path)
	{
		try
		{
			string fullPath = Path.GetFullPath(AppDomain.CurrentDomain.SetupInformation.ApplicationBase + path);
			if (File.Exists(fullPath))
			{
				ProcessStartInfo processStartInfo = new ProcessStartInfo(fullPath, AppLocalizationService.GetLang("Language"));
				processStartInfo.CreateNoWindow = false;
				processStartInfo.WorkingDirectory = fullPath.Substring(0, fullPath.LastIndexOf("\\"));
				Process.Start(processStartInfo);
			}
		}
		catch (Exception)
		{
		}
	}

	private void DeInitExit()
	{
		try
		{
			VmSolution.Instance.Dispose(isTakeOver: false);
		}
		catch (VmException ex)
		{
			LogHelper.objLog.Error("销毁句柄失败:" + ex.errorCode);
		}
		catch
		{
		}
		finally
		{
			Environment.Exit(0);
		}
	}

	internal static void ShowMainWindow()
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected Obj, but got Unknown
		Application.Current.MainWindow = (Window)new MainWindow();
		Application.Current.MainWindow.DataContext = Program.MainData;
		Application.Current.MainWindow.SetBinding(MainWindow.IsShowProgressMaskProperty, "IsShowProgressMask");
		Application.Current.MainWindow.SetBinding(MainWindow.MaskMessageProperty, "MaskMessage");
		Application.Current.MainWindow.Closing += MainWindow_Closing;
		Application.Current.MainWindow.Show();
		Program.ConfigZoomIndex();
		Program.MainData.RecoverMainWindowState(Application.Current.MainWindow);
	}

	internal static void RemoveMainWindowClosingHandle()
	{
		if (Application.Current != null && Application.Current.MainWindow != null)
		{
			Application.Current.MainWindow.Closing -= MainWindow_Closing;
		}
	}

	internal static void AddMainWindowClosingHandle()
	{
		if (Application.Current.MainWindow != null)
		{
			Application.Current.MainWindow.Closing -= MainWindow_Closing;
			Application.Current.MainWindow.Closing += MainWindow_Closing;
		}
	}

	private static void MainWindow_Closing(object sender, CancelEventArgs e)
	{
		LogHelper.Info("Main Window close.");
		e.Cancel = Program.IsCancelClose();
		if (!e.Cancel)
		{
			UILogManager.logCreate("信息", "Lang_CloseApp", true, true);
			LogHelper.Info("Window close process start.");
			Ioc.Get<IApplicationOperation>()?.Dispose();
			LogHelper.Info("Window close process end.");
			Environment.Exit(0);
		}
	}

	private VmException GetVmException(Exception exception)
	{
		VmException result = null;
		while (exception != null)
		{
			if (exception is VmException)
			{
				result = (VmException)exception;
				break;
			}
			exception = exception.InnerException;
		}
		return result;
	}

	private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		MiniDumpUtil.TryWriteMiniDump(MiniDumpType.MiniDumpNormal);
		LogHelper.Error(AppLocalizationService.GetLang("Lang_UnknowErr") + CommonHelper.GetExceptionString(e.ExceptionObject as Exception));
		if (e.ExceptionObject is VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
		}
		else if (e.ExceptionObject is OutOfMemoryException)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PrgmErr"), AppLocalizationService.GetLang("程序可用内存不足"), MessageIcons.Error);
		}
		else
		{
			MessageBox.Show(AppLocalizationService.GetLang("Lang_UnknowErr"), AppLocalizationService.GetLang("Lang_PrgmErr"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		RecordErrorInfo(e.ExceptionObject as Exception);
		DeInitExit();
	}

	[DllImport("User32")]
	private static extern int GetGuiResources(IntPtr hProcess, int uiFlags);

	private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		string text = "";
		if (e.Exception != null)
		{
			LogHelper.Error(AppLocalizationService.GetLang("Lang_UnknowErr") + CommonHelper.GetExceptionString(e.Exception));
			if (e.Exception is VmException ex)
			{
				if (ex.errorCode != -536869120)
				{
					MessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), AppLocalizationService.GetLang("Lang_PrgmErr"), MessageBoxButton.OK, MessageBoxImage.Hand);
				}
			}
			else if (e.Exception is OutOfMemoryException)
			{
				try
				{
					using (Process process = Process.GetCurrentProcess())
					{
						int guiResources = GetGuiResources(process.Handle, 0);
						int guiResources2 = GetGuiResources(process.Handle, 1);
						text = $"内存不足,此时GDI的个数为{guiResources},句柄个数为{guiResources2}";
					}
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PrgmErr"), AppLocalizationService.GetLang("程序可用内存不足"), MessageIcons.Error);
				}
				catch (Exception)
				{
					LogHelper.Error("内存不足,检测GDI及GPU" + text);
				}
			}
			else
			{
				try
				{
					MessageBox.Show(e.Exception.Message, AppLocalizationService.GetLang("Lang_PrgmErr"), MessageBoxButton.OK, MessageBoxImage.Hand);
				}
				catch (Exception)
				{
				}
			}
			RecordErrorInfo(e.Exception, text);
		}
		e.Handled = true;
	}

	private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
	{
		foreach (Exception innerException in e.Exception.InnerExceptions)
		{
			LogHelper.Error(string.Format(AppLocalizationService.GetLang("Lang_Type") + "：{0}{1}：{2}{3}" + AppLocalizationService.GetLang("RunParam_Content") + "：{4}", innerException.GetType(), Environment.NewLine, innerException.Source, Environment.NewLine, innerException.Message));
		}
		LogHelper.Error(e.Exception.InnerException.StackTrace);
		if (e.Exception.InnerException is VmException ex)
		{
			MessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex.errorCode), AppLocalizationService.GetLang("Lang_PrgmErr"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		else if (e.Exception.InnerException is OutOfMemoryException)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_PrgmErr"), AppLocalizationService.GetLang("程序可用内存不足"), MessageIcons.Error);
		}
		else
		{
			MessageBox.Show(e.Exception.InnerException.Message, AppLocalizationService.GetLang("Lang_PrgmErr"), MessageBoxButton.OK, MessageBoxImage.Hand);
		}
		e.SetObserved();
	}

	public static void RecordErrorInfo(Exception e, string ExternError = "")
	{
		if (e == null)
		{
			return;
		}
		string path = AppDomain.CurrentDomain.BaseDirectory + "\\log\\" + ExternError + "error.txt";
		TextWriter textWriter = null;
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			textWriter = File.CreateText(path);
			string exceptionString = CommonHelper.GetExceptionString(e);
			textWriter.Write(exceptionString);
			textWriter.Flush();
		}
		finally
		{
			textWriter?.Close();
		}
	}

	internal void ProcessCommandLineArgs(string[] args)
	{
		try
		{
			if (MainWindow != null)
			{
				MainWindow.Activate();
			}
			if (args == null || args.Length <= 1)
			{
				return;
			}
			string text = args[1];
			if (!string.IsNullOrEmpty(text) && File.Exists(text) && Path.GetExtension(text) == "." + AppFileExtension.Solution && !Program.MainData.SoftWareData.IsAutoLoad && !Program.MainData.SoftWareData.IsAutoLoadLast)
			{
				if (MainWindow.WindowState == WindowState.Minimized)
				{
					MainWindow.WindowState = WindowState.Normal;
				}
				ProjectManager.GetInstance().LoadSolution(text, isGuid: false, null, null, isRemoteMode: false, isFromAppStart: false, isFromAppArgs: true);
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(CommonHelper.GetExceptionString(ex));
		}
	}

	[STAThread]
	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	public static void Main()
	{
		App app = new App();
		app.Run();
	}
}
