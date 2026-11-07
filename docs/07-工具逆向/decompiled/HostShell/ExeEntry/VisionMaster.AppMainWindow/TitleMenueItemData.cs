using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using Apps.ColorFun;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Json;
using Apps.Localization;
using Apps.Log;
using Apps.PcUI;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Apps.XmlUI;
using Apps.XmlUI.ParamTab;
using Frontend.Base;
using Frontend.Base.Interface;
using FrontendUI.Design;
using FrontendUI.Design.Helper;
using GlobalScriptUI;
using Mvvm.Commands;
using PasswordWindowUI;
using UIVersionControlManager;
using VM.CalibGuide;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.User;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.Interface;
using VMControls.WPF.DataForUI;
using VMControls.WPF.GlobalUI;
using VisionMaster.Project;
using VisionMasterMain;
using VisionMasterMain.AppMainWindow;

namespace VisionMaster.AppMainWindow;

public class TitleMenueItemData : ItemData
{
	private const int CommunicationMgrID = 11000;

	public string customItemTemplateName;

	public string getCustomItemDataContextMethodName;

	private object customItemData;

	public bool isEnableByRun = false;

	private object runData = null;

	public bool isDisplayInMenu = true;

	public bool isShowSeparator;

	public string imageEnablePath;

	public string imageDisablePath;

	public bool isShowExternal;

	public GroupItemInfo toolGroupItemInfo;

	public string dynamicGetItemsMethodName;

	private bool isSolutionPath;

	private string toolTip;

	public string methodName;

	public string oldMaxModuleCount;

	public string oldIfSlientExecute;

	public string oldMVDWarmupGpuEnable;

	private DelegateCommand itemClickCommand = null;

	private const string OpenSolutionMethodName = "OpenRecentlySolution";

	private const string _itemauth = "itemauth";

	private const string _itemvmset = "itemvmset";

	private const string _itemsolset = "itemsolset";

	private const string _itemprocrunset = "itemprocrunset";

	private const string _itemcollectset = "itemcollectset";

	private static LightSourceDlg lightSourceDlg = null;

	private static GlobalCameraDlg globalCameraDialog = null;

	private static string langcur = "";

	private int _redoCount;

	private int _undoCount;

	private static GlobalVariableDialog globalVariableDialog = null;

	private static MainWindow globalScriptWindow = null;

	private static GloablTriggleConfigUI globalTriggerDialog = null;

	private static Action _userManagedAction;

	private static Func<bool?> _userLoginAction;

	private static ObservableCollection<DelegateCommand> _commands = new ObservableCollection<DelegateCommand>
	{
		new DelegateCommand(),
		new DelegateCommand(),
		new DelegateCommand()
	};

	private static bool isInit = false;

	private static TitleMenueItemData ControllerSet;

	public override IList Items
	{
		get
		{
			if (string.IsNullOrEmpty(dynamicGetItemsMethodName))
			{
				return items;
			}
			Type type = GetType();
			try
			{
				MethodInfo method = type.GetMethod(dynamicGetItemsMethodName);
				items = method.Invoke(this, null) as List<ItemData>;
				return items;
			}
			catch (Exception)
			{
				return null;
			}
		}
	}

	public string CustomItemTemplateName => customItemTemplateName;

	private string GetCustomItemDataContextMethodName => getCustomItemDataContextMethodName;

	public object CustomItemData
	{
		get
		{
			if (customItemData == null)
			{
				ExcuteMethodByName(getCustomItemDataContextMethodName);
			}
			return customItemData;
		}
		set
		{
			if (customItemData != value)
			{
				customItemData = value;
				OnPropertyChanged("CustomItemData");
			}
		}
	}

	public bool IsEnableByRun => isEnableByRun;

	public object RunData
	{
		get
		{
			if (runData == null && IsEnableByRun)
			{
				runData = VmSolution.Instance;
				VmSolution.RestoreDefaultDisplay += VmSolution_RestoreDefaultDisplay;
			}
			return runData;
		}
		set
		{
			if (runData != value && IsEnableByRun)
			{
				runData = value;
				if (runData == null)
				{
					VmSolution.RestoreDefaultDisplay -= VmSolution_RestoreDefaultDisplay;
				}
				OnPropertyChanged("RunData");
			}
		}
	}

	public virtual bool IsDisplayInMenu
	{
		get
		{
			return isDisplayInMenu;
		}
		set
		{
			if (isDisplayInMenu != value)
			{
				isDisplayInMenu = value;
				OnPropertyChanged("IsDisplayInMenu");
			}
		}
	}

	public bool IsShowSeparator
	{
		get
		{
			return isShowSeparator;
		}
		set
		{
			if (isShowSeparator != value)
			{
				isShowSeparator = value;
				OnPropertyChanged("IsShowSeparator");
			}
		}
	}

	public string ImageEnablePath => "pack://SiteOfOrigin:,,,/" + imageEnablePath;

	public string ImageDisablePath => "pack://SiteOfOrigin:,,,/" + imageDisablePath;

	public bool IsShowExternal => isShowExternal;

	public GroupItemInfo ToolGroupItemInfo => toolGroupItemInfo;

	public string DynamicGetItemsMethodName => dynamicGetItemsMethodName;

	public bool IsSolutionPath => isSolutionPath;

	public string ToolTip => toolTip;

	public string MethodName => methodName;

	public ICommand ItemClickCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = itemClickCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ItemClickCommandExecute);
				DelegateCommand val3 = val2;
				itemClickCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	private bool IsShow { get; set; } = false;

	public static ObservableCollection<DelegateCommand> Commands
	{
		get
		{
			return _commands;
		}
		set
		{
			_commands = value;
		}
	}

	public event EventHandler LangPropertyChanged;

	public void GetUnRedoStack()
	{
		CustomItemData = Ioc.Get<IUnRedoService>()?.GetScheduleInstance();
	}

	public void GetMainData()
	{
		CustomItemData = Program.MainData;
	}

	public void GetSolution()
	{
		CustomItemData = VmSolution.Instance;
		VmSolution.RestoreDefaultDisplay -= VmSolution_RestoreDefaultDisplay;
		VmSolution.RestoreDefaultDisplay += VmSolution_RestoreDefaultDisplay;
	}

	private void VmSolution_RestoreDefaultDisplay(object sender, EventArgs e)
	{
		CustomItemData = null;
		RunData = null;
	}

	private void Instance_InstanceChanged(object sender, EventArgs e)
	{
		CustomItemData = null;
		RunData = null;
	}

	private void ItemClickCommandExecute()
	{
		if (IsEnabled && MethodName != null)
		{
			ExcuteMethodByName(MethodName);
		}
	}

	protected void ExcuteMethodByName(string methodName)
	{
		Type type = GetType();
		try
		{
			MethodInfo method = type.GetMethod(methodName);
			method.Invoke(this, null);
		}
		catch (Exception)
		{
		}
	}

	public TitleMenueItemData()
		: base("VisionMaster.AppMainWindow.TitleMenueItemData")
	{
		FunctionManager.Register("GlobalLightSet", LightSet);
		FunctionManager.Register("GlobalCameraSet", CameraSet);
	}

	public void CreateNewSolution()
	{
		if (ProjectManager.GetInstance().IsCreating || ((IVmSolution)VmSolution.Instance).IsSaving || ((IVmSolution)VmSolution.Instance).IsLoading)
		{
			return;
		}
		ProjectManager.GetInstance().IsCreating = true;
		LogHelper.Info("CreateNewSolution start.");
		Task task = new Task(() =>
		{
			try
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					Program.MainData.IsShowProgressMask = true;
					Program.MainData.MaskMessage = AppLocalizationService.GetLang("Lang_NewSol");
				});
				if (GlobalCommunicationMgr.Instance.IsGet)
				{
					GlobalCommunicationMgr.Instance.CloseComm();
					GlobalCommunicationMgr.Instance.IsGet = false;
				}
				FrontendWindowData.Instance.CreateNewDesign();
				ResetResource();
				ProjectManager.GetInstance().CreateNewSolution();
				ServerSDKManager.serverSDKManager.mSolutionManager.StartGlobalScript();
				ProjectManager.GetInstance().IsCreating = false;
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					UILogManager.logCreate("信息", AppLocalizationService.GetLang("Lang_NewSol"), false, true);
				});
				if (ControllerSet != null)
				{
					ControllerSet.IsVisible = false;
				}
				LogHelper.Info("CreateNewSolution end.");
			}
			catch (Exception ex)
			{
				ProjectManager.GetInstance().IsCreating = false;
				LogHelper.Error("CreateNewSolution error: " + ex.Message + "\r\n" + ex.StackTrace);
			}
			finally
			{
				System.Windows.Application.Current.Dispatcher.Invoke(() =>
				{
					Program.MainData.IsShowProgressMask = false;
				});
			}
		});
		if (VmSolution.Instance.Modules.Count > 0 && ProjectManager.GetInstance().NeedShowSaveNotice)
		{
			switch (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_SolSave"), AppLocalizationService.GetLang("Lang_SolCurSave"), MessageButtons.YesOrNoOrCancel, MessageIcons.Question))
			{
			case VM.Controls.DialogResult.Yes:
			{
				LogHelper.Info("CreateNewSolution SaveSolution.");
				bool isRemoteSave = false;
				if (!ProjectManager.GetInstance().SaveSolution(task, bDisplayProgress: true, isRemoteSave))
				{
					ProjectManager.GetInstance().IsCreating = false;
				}
				return;
			}
			default:
				ProjectManager.GetInstance().IsCreating = false;
				return;
			case VM.Controls.DialogResult.No:
				break;
			}
		}
		task.Start();
	}

	public static void ResetResource()
	{
		System.Windows.Application.Current?.Dispatcher.Invoke(() =>
		{
			GlobalCommunicationMgr.globalCommunication?.Close();
			GlobalCommunicationMgr.globalCommunication = null;
			GlobalCameraManager.Instance.CloseCamera();
			globalCameraDialog?.Close();
			globalCameraDialog = null;
			lightSourceDlg?.Close();
			lightSourceDlg = null;
			globalTriggerDialog?.Close();
			globalTriggerDialog = null;
			((BaseWindow)(object)globalScriptWindow)?.Close();
			globalScriptWindow = null;
			globalVariableDialog?.Close();
			globalVariableDialog = null;
		});
	}

	public static void OpenSolution()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			StopRun();
			LogHelper.Info("OpenSolution");
			UILogManager.logCreate("信息", "Lang_OpenSol", true, true);
			ProjectManager.GetInstance().LoadSolution();
		}
	}

	public static void OpenSolutionNative()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			StopRun();
			LogHelper.Info("OpenSolution");
			ProjectManager.GetInstance().LoadSolution();
		}
	}

	public static void OpenSolutionRemote()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			StopRun();
			LogHelper.Info("OpenSolution");
			UILogManager.logCreate("信息", "远程打开方案", true, true);
			ProjectManager.GetInstance().LoadSolution(null, isGuid: false, null, null, isRemoteMode: true);
		}
	}

	public void OpenRecentlySolution()
	{
		OpenRecentSolution(ToolTip);
	}

	private static bool OpenRecentSolution(string path)
	{
		if (ProjectManager.GetInstance().IsCreating || ((IVmSolution)VmSolution.Instance).IsSaving || ((IVmSolution)VmSolution.Instance).IsLoading)
		{
			return false;
		}
		StopRun();
		LogHelper.Info("OpenRecentSolution path: " + path);
		UILogManager.logCreate("信息", AppLocalizationService.GetLang("Lang_OpenLastSol") ?? "", false, true);
		return ProjectManager.GetInstance().LoadSolution(path);
	}

	public static List<ItemData> GetRecentlySolutions()
	{
		return ProjectManager.GetInstance().RecentlySolutions.Select((SolutionItemInfo p) => new TitleMenueItemData
		{
			name = p.FileName,
			methodName = "OpenRecentlySolution",
			isSolutionPath = true,
			toolTip = p.SolutionPath
		}).OfType<ItemData>().ToList();
	}

	public void RefreshRecentSolution()
	{
		OnPropertyChanged("Items");
	}

	public void OpenSamples()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("OpenSamples");
			UILogManager.logCreate("信息", "Lang_OpenExmSol", true, true);
			string path = default;
			GuideHelper.OpenSolution(true, ref path);
			if (File.Exists(path))
			{
				LoadSolution(path);
			}
			CloseGlobalScriptWindow();
		}
	}

	public static void LoadSolution(string path)
	{
		StopRun();
		LogHelper.Info("OpenSolution");
		ProjectManager.GetInstance().LoadSolution(path);
	}

	public void SaveSolution()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("SaveSolution");
			if (ProjectManager.GetInstance().SaveSolution())
			{
				GlobalCommunicationMgr.Instance.ResetParamStateFlag();
			}
		}
	}

	public void SaveSolutionNative()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("SaveSolution");
			if (ProjectManager.GetInstance().SaveSolution())
			{
				GlobalCommunicationMgr.Instance.ResetParamStateFlag();
			}
		}
	}

	public void SaveSolutionRemote()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("SaveSolution");
			UILogManager.logCreate("信息", "远程保存方案", true, true);
			if (ProjectManager.GetInstance().SaveSolution(null, bDisplayProgress: true, isRemoteSave: true))
			{
				GlobalCommunicationMgr.Instance.ResetParamStateFlag();
			}
		}
	}

	public static void SaveAsSolution()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("SaveAsSolution");
			UILogManager.logCreate("信息", "Lang_SaveAsSol", true, true);
			if (ProjectManager.GetInstance().AnotherSaveSolution())
			{
				GlobalCommunicationMgr.Instance.ResetParamStateFlag();
			}
		}
	}

	public void SaveAsSolutionNative()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("SaveAsSolutionNative");
			UILogManager.logCreate("信息", "Lang_SaveAsSol", true, true);
			if (ProjectManager.GetInstance().AnotherSaveSolution())
			{
				GlobalCommunicationMgr.Instance.ResetParamStateFlag();
			}
		}
	}

	public void SaveAsSolutionRemote()
	{
		if (!ProjectManager.GetInstance().IsCreating && !((IVmSolution)VmSolution.Instance).IsSaving && !((IVmSolution)VmSolution.Instance).IsLoading)
		{
			LogHelper.Info("SaveAsSolutionRemote");
			UILogManager.logCreate("信息", "Lang_SaveAsSol", true, true);
			if (ProjectManager.GetInstance().AnotherSaveSolution(null, bDisplayProgress: true, isRemoteSave: true))
			{
				GlobalCommunicationMgr.Instance.ResetParamStateFlag();
			}
		}
	}

	public void ImportProcess()
	{
		DecoratorBase.ImportProcedure(default);
	}

	public void ImportProcessNative()
	{
		DecoratorBase.ImportProcedure(default);
	}

	public void ImportProcessRemote()
	{
		DecoratorBase.ImportProcedure(default, isRemoteImport: true);
	}

	public void ExportAlgorithm()
	{
		if (UIVersionControlManagerCS.IsDongleLowerThanConfig(UIVersionControlManagerCS.ProLevel))
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("errCode:") + "0xE000071D" + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(-536869091), MessageIcons.Error);
			return;
		}
		IEnumerable<VmProcedureDecorator> source = from p in VmSolution.Instance.Modules.OfType<VmProcedure>()
			select new VmProcedureDecorator
			{
				DecorateSource = p
			};
		VmProcedureDecorator vmProcedureDecorator;
		if ((vmProcedureDecorator = source.FirstOrDefault((VmProcedureDecorator p) => p.WorkState != StatusBarSymbol.Ready)) != null)
		{
			string strMsg = vmProcedureDecorator.VMProcedure.Name + ":" + vmProcedureDecorator.WorkStateInfo;
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ExportFailed"), strMsg, MessageIcons.Error);
		}
		else
		{
			uint iD = VmSolution.Instance.ID;
			ExportAlgorithmWindow.ShowDialog(iD, true);
		}
	}

	public static void Exit()
	{
		if (System.Windows.Application.Current.MainWindow != null)
		{
			System.Windows.Application.Current.MainWindow?.Close();
			return;
		}
		UILogManager.logCreate("信息", "Lang_CloseApp", true, true);
		LogHelper.Info("Window close process start.");
		Ioc.Get<IApplicationOperation>()?.Dispose();
		LogHelper.Info("Window close process end.");
		Environment.Exit(0);
	}

	public void CloseGlobalScriptWindow()
	{
		if (globalScriptWindow != null)
		{
			((Window)(object)globalScriptWindow).Hide();
		}
	}

	private void Softwarepage_NotifySolutionPathChanged()
	{
		Program.MainData.OnPropertyChanged("SolutionPath");
		Program.MainData.OnPropertyChanged("SolutionName");
	}

	private void SoftwareSetDlgShow(string item)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		SoftwareSetDlg val = new SoftwareSetDlg();
		val.ApplyEventHander += Sdg_ApplyEventHander;
		((FrameworkElement)(object)val.solpage).DataContext = Program.MainData.SolutionData;
		((FrameworkElement)(object)val.collectionPage).DataContext = Program.MainData.SoftWareData;
		Program.MainData.SoftWareData.LoadSolParam();
		Program.MainData.ProcRunPolicyData.LoadRunPolicyConfig();
		((FrameworkElement)(object)val.procedureRunPage).DataContext = Program.MainData.ProcRunPolicyData;
		Program.MainData.SoftWareData.LoadSoftWareInfo();
		((FrameworkElement)(object)val.softwarepage).DataContext = Program.MainData.SoftWareData;
		((FrameworkElement)(object)val.accessPage).DataContext = Program.MainData.SoftWareData;
		val.ChangePageIsChecked(item);
		switch (item)
		{
		case "itemauth":
			val.ChangePage((Page)(object)val.accessPage);
			break;
		case "itemvmset":
			val.ChangePage((Page)(object)val.softwarepage);
			break;
		case "itemsolset":
			val.ChangePage((Page)(object)val.solpage);
			break;
		case "itemprocrunset":
			val.ChangePage((Page)(object)val.procedureRunPage);
			break;
		case "itemcollectset":
			val.ChangePage((Page)(object)val.collectionPage);
			break;
		}
		((BaseWindow)(object)val).ShowDialog();
	}

	private bool Sdg_ApplyEventHander()
	{
		if (Program.MainData.SoftWareData.GetIsRestart())
		{
			if (VM.Controls.DialogResult.Yes == HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ReStartVM"), MessageButtons.YesOrNo, MessageIcons.Question))
			{
				Program.MainData.SolutionData.SaveSolInfo();
				Program.MainData.ProcRunPolicyData.SetRunPolicyConfig();
				Program.MainData.SoftWareData.SaveSoftWareInfo();
				Program.MainData.SoftWareData.SaveAccessInfo();
				ReStartVM();
			}
			return true;
		}
		Program.MainData.SolutionData.SaveSolInfo();
		Program.MainData.ProcRunPolicyData.SetRunPolicyConfig();
		Program.MainData.SoftWareData.SaveSoftWareInfo();
		Program.MainData.SoftWareData.SaveAccessInfo();
		return true;
	}

	public void ReStartVM()
	{
		string text = ConfigurationHelper.GetConfig("ApplicationName") + ".exe";
		string text2 = Environment.CurrentDirectory + "\\";
		string arguments = "\"" + text + "\" \"" + text2 + "\"";
		Process process = new Process();
		process.StartInfo.WorkingDirectory = text2;
		process.StartInfo.FileName = "ReStartVM.bat";
		process.StartInfo.Arguments = arguments;
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
		process.Start();
		process.WaitForExit();
	}

	public void AuthSet()
	{
		SoftwareSetDlgShow("itemauth");
	}

	public void VMSet()
	{
		SoftwareSetDlgShow("itemvmset");
	}

	public void Solset()
	{
		SoftwareSetDlgShow("itemsolset");
	}

	public void ProcRunSet()
	{
		SoftwareSetDlgShow("itemprocrunset");
	}

	public void CollectSet()
	{
		SoftwareSetDlgShow("itemcollectset");
	}

	public void LogShow()
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected Obj, but got Unknown
		UILogManager val = new UILogManager(true);
		((BaseWindow)(object)val).ShowDialog();
	}

	public static void CommSet()
	{
		//IL_0262: Unknown result type (might be due to invalid IL or missing references)
		//IL_0268: Invalid comparison between Unknown and I4
		//IL_01f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Invalid comparison between Unknown and I4
		try
		{
			if (Program.MainData.IsShowProgressMask)
			{
				return;
			}
			Program.MainData.IsShowProgressMask = true;
			Program.MainData.MaskMessage = AppLocalizationService.GetLang("Lang_LoadingCommManagerModule");
			System.Windows.Forms.Application.DoEvents();
			VmModule vmModule = null;
			vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 11000);
			if (CheckConfigAvailable(vmModule))
			{
				return;
			}
			if (vmModule == null)
			{
				string path = CurrentPathHelper.Instance.GetCurrentPath() + "module(sp)\\x64\\Global\\CommManagerModule\\CommManagerModuleCs.dll";
				Assembly assembly = Assembly.LoadFile(path);
				Type type = assembly.GetTypes().FirstOrDefault((Type p) => typeof(IVmModule).IsAssignableFrom(p));
				Activator.CreateInstance(type, VmSolution.Instance);
				vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 11000);
				if (GlobalCommunicationMgr.globalCommunication != null)
				{
					GlobalCommunicationMgr.globalCommunication = null;
				}
			}
			if (vmModule == null)
			{
				GlobalCommunicationMgr.globalCommunication = new GlobalCommunication();
			}
			if (vmModule != null)
			{
				if (GlobalCommunicationMgr.globalCommunication == null)
				{
					GlobalCommunicationMgr.globalCommunication = new GlobalCommunication();
				}
				if (GlobalCommunicationMgr.globalCommunication.IsClosing)
				{
					GlobalCommunicationMgr.globalCommunication = new GlobalCommunication();
				}
				GlobalCommunicationMgr.globalCommunication.DecorateSource = vmModule;
				if (!GlobalCommunicationMgr.Instance.IsGet)
				{
					GlobalCommunicationMgr.Instance.IsGet = true;
					GlobalCommunicationMgr.Instance.GetData();
					GlobalCommunicationMgr.globalCommunication.UpdateLanguage(string.IsNullOrEmpty(langcur) ? AppLocalizationService.GetLangParm() : langcur);
					GlobalCommunicationMgr.globalCommunication.SetTechOperate((int)CodeManager.Instance.GetLoginUser() == 2);
					GlobalCommunicationMgr.Instance.IsOpen = true;
					GlobalCommunicationMgr.Instance.ResetParamStateFlag();
					Program.MainData.IsShowProgressMask = false;
					GlobalCommunicationMgr.globalCommunication.ShowDialog();
					return;
				}
				GlobalCommunicationMgr.globalCommunication.UpdateLanguage(string.IsNullOrEmpty(langcur) ? AppLocalizationService.GetLangParm() : langcur);
				GlobalCommunicationMgr.globalCommunication.SetTechOperate((int)CodeManager.Instance.GetLoginUser() == 2);
				if (GlobalCommunicationMgr.globalCommunication.Visibility != Visibility.Visible)
				{
					GlobalCommunicationMgr.Instance.IsOpen = true;
					Program.MainData.IsShowProgressMask = false;
					GlobalCommunicationMgr.globalCommunication.ShowDialog();
				}
			}
			Program.MainData.IsShowProgressMask = false;
		}
		catch (Exception ex)
		{
			Program.MainData.IsShowProgressMask = false;
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				ShowMessage.ShowVmExceptionMessage(ex2);
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.Error(ex.Message);
			}
		}
	}

	public static void LightSet()
	{
		try
		{
			VmModule vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 14000);
			if (!CheckConfigAvailable(vmModule))
			{
				if (vmModule == null)
				{
					CommSet();
				}
				else if (vmModule != null)
				{
					lightSourceDlg = new LightSourceDlg();
					LightSourceManager.Instance.ModuleID = (int)vmModule.ID;
					LightSourceManager.Instance.GetAuthority();
					LightSourceManager.Instance.GetData();
					LightSourceManager.Instance.ReFreshComNum();
					lightSourceDlg.ShowDialog();
					DeleteOldLightModule();
				}
			}
		}
		catch (Exception ex)
		{
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				ShowMessage.ShowVmExceptionMessage(ex2);
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.Error(ex.Message);
			}
		}
	}

	public static void CameraSet()
	{
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Invalid comparison between Unknown and I4
		try
		{
			if (!Program.IsSdkMode() && (int)CodeManager.Instance.GetLoginUser() == 3)
			{
				return;
			}
			List<VmModule> list = VmSolution.Instance.Modules.ToList();
			foreach (VmModule item in list)
			{
				if (GlobalCameraManager.Instance.FilterCameraModule(item.StrModuleName) && !GlobalCameraManager.Instance.ItemsA.Contains(item))
				{
					GlobalCameraManager.Instance.ItemsA.Add(item);
				}
			}
			List<VmModule> list2 = GlobalCameraManager.Instance.ItemsA.ToList();
			foreach (VmModule item2 in list2)
			{
				if (!VmSolution.Instance.Modules.Contains(item2))
				{
					GlobalCameraManager.Instance.ItemsA.Remove(item2);
					GlobalCameraManager.Instance.CurDeviceA = null;
				}
			}
			globalCameraDialog = new GlobalCameraDlg();
			GlobalCameraManager.Instance.GetAuthority();
			globalCameraDialog.ShowDialog();
		}
		catch (Exception ex)
		{
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				ShowMessage.ShowVmExceptionMessage(ex2);
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				LogHelper.Error(ex.Message);
			}
		}
	}

	public void CreateCalibGuide()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		TypeSelectWindow val = new TypeSelectWindow();
		((BaseWindow)(object)val).ShowDialog();
	}

	public void OnLangPropertyChanged()
	{
		if (LangPropertyChanged != null)
		{
			LangPropertyChanged(this, new EventArgs());
		}
	}

	public void ChSet()
	{
		if (!(langcur == "zh-cn"))
		{
			string language = "zh-cn";
			AppLocalizationService.ChangeLanguage(language);
			(GetTopBaseNode() as TitleMenueItemData)?.OnLangPropertyChanged();
			langcur = "zh-cn";
			Program.SetAuthToolLanguage(isChinese: true);
		}
	}

	public void ChTraditionSet()
	{
		if (!(langcur == "zh-tw"))
		{
			string language = "zh-tw";
			AppLocalizationService.ChangeLanguage(language);
			(GetTopBaseNode() as TitleMenueItemData)?.OnLangPropertyChanged();
			langcur = "zh-tw";
			Program.SetAuthToolLanguage(isChinese: true);
		}
	}

	public void EnSet()
	{
		if (!(langcur == "en-us"))
		{
			string language = "en-us";
			AppLocalizationService.ChangeLanguage(language);
			(GetTopBaseNode() as TitleMenueItemData)?.OnLangPropertyChanged();
			langcur = "en-us";
			Program.SetAuthToolLanguage(isChinese: false);
		}
	}

	public void HelpDoc()
	{
		GuideHelper.GetHelp(true);
	}

	public void OnlineHelpDoc()
	{
		string config = ConfigurationHelper.GetConfig("OnlineHelpDoc");
		Process.Start(config);
	}

	public void VerInfo()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		AboutMessageBox val = new AboutMessageBox();
		((BaseWindow)(object)val).ShowDialog();
	}

	public void OpenVClub()
	{
		string config = ConfigurationHelper.GetConfig("VClub");
		Process.Start(config);
	}

	public void MoreInfo()
	{
		GuideHelper.GetHelp(false);
	}

	public void WecPage()
	{
		Program.WecPage();
	}

	public void Undo()
	{
		if (_undoCount > 0)
		{
			return;
		}
		try
		{
			_undoCount++;
			if (Program.MainData.IsOpenParams)
			{
				HikMessageBox.Show("打开模块配置时，不能执行撤消重做", MessageIcons.Warning, isTranslate: true);
				return;
			}
			Ioc.Get<IUnRedoService>()?.Undo(System.Windows.Application.Current.Dispatcher);
			UILogManager.logCreate("信息", AppLocalizationService.GetLang("执行撤消操作"), false, true);
		}
		catch (Exception)
		{
		}
		finally
		{
			_undoCount--;
		}
	}

	public void Redo()
	{
		if (_redoCount > 0)
		{
			return;
		}
		try
		{
			_redoCount++;
			if (Program.MainData.IsOpenParams)
			{
				HikMessageBox.Show("打开模块配置时，不能执行撤消重做", MessageIcons.Warning, isTranslate: true);
				return;
			}
			Ioc.Get<IUnRedoService>()?.Redo(System.Windows.Application.Current.Dispatcher);
			UILogManager.logCreate("信息", AppLocalizationService.GetLang("执行重做操作"), false, true);
		}
		finally
		{
			_redoCount--;
		}
	}

	public void AreaLocking()
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Expected Obj, but got Unknown
		if (!Program.MainData.IsNotifyLock)
		{
			LockDlg val = new LockDlg(false);
			((BaseWindow)(object)val).ShowDialog();
			Program.MainData.IsNotifyLock = val.IsNotifyLock;
			if (((Window)(object)val).DialogResult ?? false)
			{
				Program.MainData.IsWorkAreaLocking = true;
			}
		}
		else
		{
			Program.MainData.IsWorkAreaLocking = true;
		}
	}

	public void AreaUnLocking()
	{
		Program.MainData.IsWorkAreaLocking = false;
	}

	public void UndoList()
	{
		if (IsShow)
		{
			return;
		}
		BaseWindow baseWindow = new BaseWindow
		{
			ShowInTaskbar = false
		};
		if (System.Windows.Application.Current != null)
		{
			baseWindow.Owner = System.Windows.Application.Current.MainWindow;
			baseWindow.SetResourceReference(Window.TitleProperty, "Lang_Records");
			baseWindow.Resources.MergedDictionaries.Add((ResourceDictionary)(object)LocalizationResourceDictionary.Instance);
		}
		baseWindow.Height = 420.0;
		baseWindow.Width = ((AppLocalizationService.GetLangParm() == "zh-cn") ? 242 : 363);
		baseWindow.FontSize = 14.0;
		baseWindow.FontWeight = FontWeights.Normal;
		baseWindow.Topmost = true;
		baseWindow.MinButtonVisibility = Visibility.Collapsed;
		System.Windows.Controls.UserControl content = Ioc.Get<IUnRedoService>()?.GetUnRedoView();
		ColorThemesResourceDictionary.GetInstance(baseWindow);
		baseWindow.Content = content;
		baseWindow.Show();
		using (Graphics graphics = Graphics.FromHwnd(IntPtr.Zero))
		{
			if (System.Windows.Application.Current != null)
			{
				System.Windows.Point point = System.Windows.Application.Current.MainWindow.PointToScreen(new System.Windows.Point(System.Windows.Application.Current.MainWindow.ActualWidth, 0.0));
				float dpiX = graphics.DpiX;
				float dpiY = graphics.DpiY;
				WpfScreen screenFrom = WpfScreen.GetScreenFrom(baseWindow);
				double num = point.X * 96.0 / (double)dpiX - System.Windows.Application.Current.MainWindow.ActualWidth / 2.0 - baseWindow.ActualWidth - 12.0;
				double num2 = (screenFrom.WorkingArea.Height * 96.0 / (double)dpiY - baseWindow.ActualHeight) / 2.0 + screenFrom.WorkingArea.Top * 96.0 / (double)dpiY;
				if (num > screenFrom.WorkingArea.Width * 96.0 / (double)dpiX)
				{
					num = screenFrom.WorkingArea.Width * 96.0 / (double)dpiX - 20.0;
				}
				if (num2 > screenFrom.WorkingArea.Height * 96.0 / (double)dpiY)
				{
					num2 = screenFrom.WorkingArea.Height * 96.0 / (double)dpiY - 20.0;
				}
				baseWindow.Left = num;
				baseWindow.Top = num2;
			}
		}
		baseWindow.Closing += UnredoWindow_Closing;
		IsShow = true;
	}

	private void UnredoWindow_Closing(object sender, CancelEventArgs e)
	{
		IsShow = false;
	}

	public void OutputConfig()
	{
		OutputConfigWindow outputConfigWindow = new OutputConfigWindow();
		outputConfigWindow.ShowDialog();
	}

	public static void GlobalVariable()
	{
		try
		{
			VmModule globalVarModule = VmModuleExtern.GetGlobalVarModule();
			if (CheckConfigAvailable(globalVarModule))
			{
				return;
			}
			if (globalVarModule == null)
			{
				string path = CurrentPathHelper.Instance.GetCurrentPath() + "module(sp)\\x64\\Global\\GlobalVariableModule\\GlobalVariableModuleCs.dll";
				Assembly assembly = Assembly.LoadFile(path);
				Type type = assembly.GetTypes().FirstOrDefault((Type p) => typeof(IVmModule).IsAssignableFrom(p));
				Activator.CreateInstance(type, VmSolution.Instance);
				globalVarModule = VmModuleExtern.GetGlobalVarModule();
			}
			if (globalVarModule != null)
			{
				globalVariableDialog = new GlobalVariableDialog();
				globalVariableDialog.DecorateSource = globalVarModule;
				globalVariableDialog.ShowDialog();
			}
		}
		catch (Exception ex)
		{
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				ShowMessage.ShowVmExceptionMessage(ex2);
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.Error(ex.Message);
			}
		}
	}

	public void GlobalScript()
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Expected Obj, but got Unknown
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Expected Obj, but got Unknown
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Expected Obj, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected Obj, but got Unknown
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected Obj, but got Unknown
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Expected Obj, but got Unknown
		try
		{
			if (globalScriptWindow == null)
			{
				globalScriptWindow = new MainWindow();
				globalScriptWindow.ExcuteEvent += SigRun;
			}
			if (((BaseWindow)(object)globalScriptWindow).IsClosing)
			{
				globalScriptWindow.ExcuteEvent -= SigRun;
				globalScriptWindow = new MainWindow();
				globalScriptWindow.ExcuteEvent += SigRun;
			}
			if (globalScriptWindow.GetShellParamer() != 0)
			{
				return;
			}
			if (globalScriptWindow.ObjShellRun != null && globalScriptWindow.ObjShellRun.IsPasswordEnabled)
			{
				string password = globalScriptWindow.ObjShellRun.Password;
				PasswordCheckWindow val = new PasswordCheckWindow
				{
					OriginalPassword = password
				};
				((BaseWindow)(object)val).ShowDialog();
				if (val.IsPasswordOK)
				{
					((BaseWindow)(object)globalScriptWindow).ShowDialog();
				}
			}
			else
			{
				((BaseWindow)(object)globalScriptWindow).ShowDialog();
			}
		}
		catch (Exception ex)
		{
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				ShowMessage.ShowVmExceptionMessage(ex2);
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.Error(ex.Message);
			}
		}
	}

	public static void VmSolution_OnGlobalSciptReportData(object sender, ObjectEventArgs args)
	{
		if (args == null || args.Param == null || string.IsNullOrEmpty(args.Param.ToString()))
		{
			return;
		}
		string info = args.Param.ToString();
		System.Windows.Application.Current.Dispatcher.BeginInvoke((Action)(() =>
		{
			try
			{
				dynamic val = JsonConvert.DeserializeObject(info);
				if (val == null)
				{
					LogHelper.objLog.Error("DeserializeObject error,data:" + info);
				}
				else
				{
					dynamic val2 = val.head;
					if (val2 == null)
					{
						LogHelper.objLog.Error("DeserializeObject msg miss head,data:" + info);
					}
					else
					{
						string s = val.head.errorCode;
						string text = val.head.type;
						string text2 = text;
						if (!(text2 == "report"))
						{
							if (text2 == "updateScript" && globalScriptWindow != null)
							{
								string text3 = val.head.msg;
								if (!string.IsNullOrEmpty(text3))
								{
									globalScriptWindow.UpdateShellContent(text3);
								}
							}
						}
						else
						{
							int result = 0;
							int.TryParse(s, out result);
							string text4 = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(result);
							HikMessageBox.Show(text4, MessageIcons.Error);
							LogHelper.objLog.Error(text4);
						}
					}
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error("GlobalSciptReportData exception:" + ex.Message);
			}
		}));
	}

	private static void LoadGlobalTrigger()
	{
		try
		{
			VmModule vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 14200);
			if (CheckConfigAvailable(vmModule))
			{
				return;
			}
			if (vmModule == null)
			{
				string path = CurrentPathHelper.Instance.GetCurrentPath() + "module(sp)\\x64\\Global\\GlobalTriggerModule\\GlobalTriggerModuleCs.dll";
				Assembly assembly = Assembly.LoadFile(path);
				Type type = assembly.GetTypes().FirstOrDefault((Type p) => typeof(IVmModule).IsAssignableFrom(p));
				Activator.CreateInstance(type, VmSolution.Instance);
				vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 14200);
			}
			if (vmModule != null)
			{
				GlobalTriggerConfig globalTriggerConfig = new GlobalTriggerConfig();
				globalTriggerConfig.DecorateSource = vmModule;
				globalTriggerDialog = new GloablTriggleConfigUI(vmModule);
				globalTriggerDialog.DataContext = globalTriggerConfig;
				globalTriggerDialog.ShowDialog();
			}
		}
		catch (Exception ex)
		{
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				ShowMessage.ShowVmExceptionMessage(ex2);
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.Error(ex.Message);
			}
		}
	}

	public void GlobalTrigger()
	{
		LoadGlobalTrigger();
	}

	private static bool CheckConfigAvailable(VmModule module)
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Invalid comparison between Unknown and I4
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		if (Program.IsSdkMode())
		{
			return false;
		}
		AccessUser loginUser = CodeManager.Instance.GetLoginUser();
		if ((int)CodeManager.Instance.GetLoginUser() == 3)
		{
			return true;
		}
		return (int)loginUser == 2 && (module == null || !module.ConfigAvailable);
	}

	private static bool CheckRun()
	{
		IEnumerable<VmProcedureDecorator> enumerable = from p in VmSolution.Instance.Modules.OfType<VmProcedure>()
			select new VmProcedureDecorator
			{
				DecorateSource = p
			};
		VmProcedureDecorator vmProcedureDecorator;
		if ((vmProcedureDecorator = enumerable.FirstOrDefault((VmProcedureDecorator p) => p.WorkState != StatusBarSymbol.Ready && p.IsEnabled)) != null)
		{
			string strMsg = vmProcedureDecorator.VMProcedure.Name + ":" + vmProcedureDecorator.WorkStateInfo;
			HikMessageBox.Show(strMsg, MessageIcons.Error);
			return false;
		}
		if (enumerable != null && enumerable.Where((VmProcedureDecorator p) => !p.IsEnabled).Count() == enumerable.Count())
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("流程处于禁用状态"), MessageIcons.Warning);
			return false;
		}
		return true;
	}

	public static void SigRun()
	{
		try
		{
			if (CheckRun())
			{
				VmSolution.Instance.Run();
				UILogManager.logCreate("信息", AppLocalizationService.GetLang("Run Once"), false, true);
			}
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				string strMsg = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
				HikMessageBox.Show(strMsg, MessageIcons.Error);
			}
		}
	}

	public static void MutiRun()
	{
		try
		{
			if (CheckRun())
			{
				MutiOrStopRun(isContinousRunEnable: true);
				UILogManager.logCreate("信息", AppLocalizationService.GetLang("连续运行开始"), false, true);
			}
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				string strMsg = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
				HikMessageBox.Show(strMsg, MessageIcons.Error);
			}
		}
	}

	private static void MutiOrStopRun(bool isContinousRunEnable)
	{
		((IVMRun)VmSolution.Instance).ContinuousRunEnable = isContinousRunEnable;
	}

	public static void StopRun()
	{
		MutiOrStopRun(isContinousRunEnable: false);
	}

	public void GlobalScriptSigRun()
	{
		int num = ServerSDKManager.serverSDKManager.mGlobalModuleManager.GlobalScriptStartRunOnce();
		UILogManager.logCreate("信息", AppLocalizationService.GetLang("全局脚本执行一次"), false, true);
	}

	public void GlobalScriptMutiRun()
	{
		if (ServerSDKManager.serverSDKManager.mGlobalModuleManager.GlobalScriptStartContinueRun() == 0)
		{
			VmSolution.Instance.GloalScriptContinusRunEnable = true;
			UILogManager.logCreate("信息", AppLocalizationService.GetLang("全局脚本连续运行开始"), false, true);
		}
	}

	public void GlobalScriptStopRun()
	{
		if (VmSolution.Instance.GloalScriptContinusRunEnable)
		{
			if (ServerSDKManager.serverSDKManager.mGlobalModuleManager.GlobalScriptStopRun() == 0)
			{
				VmSolution.Instance.GloalScriptContinusRunEnable = false;
				UILogManager.logCreate("信息", AppLocalizationService.GetLang("停止运行"), false, true);
			}
		}
		else
		{
			StopRun();
		}
	}

	public void RunSet()
	{
	}

	public void EditFrontendUI()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		EditFrontendWindow val = new EditFrontendWindow();
		if (System.Windows.Application.Current != null && System.Windows.Application.Current.MainWindow != null)
		{
			((Window)(object)val).Owner = System.Windows.Application.Current.MainWindow;
		}
		if (!((BaseWindow)(object)val).ShowDialog().Value)
		{
		}
	}

	public void ComprehensiveConfig()
	{
		ComprehensiveConfigManager.ShowWindow();
	}

	public static List<ToolFunctionSourceModle> GetToolFunctionSources()
	{
		Init();
		List<ToolFunctionSourceModle> list = new List<ToolFunctionSourceModle>();
		ToolFunctionSourceModle toolFunctionSourceModle = new ToolFunctionSourceModle();
		toolFunctionSourceModle.strPubAddress = "tool";
		List<FunctionInfo> list2 = new List<FunctionInfo>();
		list2.Add(new FunctionInfo
		{
			FilterName = "solutionActions",
			Type = "readonly",
			Value = new object[6]
			{
				new Action(OpenSolution),
				new Func<string, bool>(OpenRecentSolution),
				new Action<string>(ProjectManager.RemoveRecentSolution),
				new Action(SolSet),
				new Action(OpenSolutionRemote),
				new Action(OpenSolutionNative)
			}
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "doclose",
			Type = "readonly",
			Value = new Action(Exit)
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "getrecentlysolution",
			Value = (Func<List<SolutionFileInfo>>)(() => ((IEnumerable<SolutionItemInfo>)ProjectManager.GetInstance().RecentlySolutions).Select((Func<SolutionItemInfo, SolutionFileInfo>)((SolutionItemInfo s) =>
			{
				//IL_0000: Unknown result type (might be due to invalid IL or missing references)
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_0012: Unknown result type (might be due to invalid IL or missing references)
				//IL_0020: Expected Obj, but got Unknown
				return new SolutionFileInfo
				{
					FileName = s.FileName,
					FilePath = s.SolutionPath
				};
			})).ToList())
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "controllercommands",
			Type = "readonly",
			Value = Commands
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "solutionManager",
			Type = "readonly",
			Value = new Action(SolSet)
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "usermanagercommand",
			Type = "readonly",
			Value = new DelegateCommand(_userManagedAction)
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "userlogincommand",
			Type = "readonly",
			Value = new Func<bool?>(_userLoginAction.Invoke)
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "hidewindow",
			Type = "readonly",
			Value = (Action)(() =>
			{
				Task.Run(() =>
				{
					System.Windows.Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
					{
						if (System.Windows.Application.Current.MainWindow != null)
						{
							System.Windows.Application.Current.MainWindow.Hide();
						}
					}));
				});
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "showwindow",
			Type = "readonly",
			Value = (Action)(() =>
			{
				System.Windows.Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
				{
					if (System.Windows.Application.Current.MainWindow is MainWindow)
					{
						System.Windows.Application.Current.MainWindow.Show();
					}
					else
					{
						App.ShowMainWindow();
					}
				}));
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "getloginuser",
			Type = "readonly",
			Value = (Func<int>)(() =>
			{
				//IL_0005: Unknown result type (might be due to invalid IL or missing references)
				//IL_000b: Expected I4, but got Unknown
				return (int)CodeManager.GetInstance().GetLoginUser();
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "codemanager",
			Type = "readonly",
			Value = CodeManager.GetInstance()
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "projectmanager",
			Type = "readonly",
			Value = ProjectManager.GetInstance()
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "savesolution",
			Type = "readonly",
			Value = (Func<string, int>)((string path) =>
			{
				ProjectManager.GetInstance().SaveSolution();
				return 0;
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "saveremotesolution",
			Type = "readonly",
			Value = (Func<string, int>)((string path) =>
			{
				ProjectManager.GetInstance().SaveSolution(null, bDisplayProgress: true, isRemoteSave: true);
				return 0;
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "savelocalsolution",
			Type = "readonly",
			Value = (Func<string, int>)((string path) =>
			{
				ProjectManager.GetInstance().SaveSolution();
				return 0;
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "showGlobalCommunicationSet",
			Type = "readonly",
			Value = (Action)(() =>
			{
				CommSet();
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "showGlobalCameraManager",
			Type = "readonly",
			Value = (Action)(() =>
			{
				CameraSet();
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "showGlobalLightSourceManagement",
			Type = "readonly",
			Value = (Action)(() =>
			{
				LightSet();
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "showGlobalVariableManagement",
			Type = "readonly",
			Value = (Action)(() =>
			{
				GlobalVariable();
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "globalvariableselectdlg",
			Type = "readonly",
			Value = (Func<string, string>)((string type) =>
			{
				//IL_0002: Unknown result type (might be due to invalid IL or missing references)
				//IL_0008: Expected Obj, but got Unknown
				GlobalVariableSelectDlg val = new GlobalVariableSelectDlg(true);
				val.Type = new string[1] { type };
				((BaseWindow)(object)val).ShowDialog();
				return val.IsConfirm ? val.SelectedGlobalVar : "";
			})
		});
		list2.Add(new FunctionInfo
		{
			FilterName = "SaveAsSolution",
			Type = "readonly",
			Value = (Action)(() =>
			{
				Task.Run(() =>
				{
					System.Windows.Application.Current.Dispatcher?.BeginInvoke((Action)(() =>
					{
						SaveAsSolution();
					}));
				});
			})
		});
		List<FunctionInfo> functionInfos = list2;
		toolFunctionSourceModle.FunctionInfos = functionInfos;
		list.Add(toolFunctionSourceModle);
		return list;
	}

	internal IList<IDataSourceHost> GetBindHost()
	{
		List<ToolFunctionSourceModle> source = new List<ToolFunctionSourceModle>();
		return ((IEnumerable<ToolFunctionSourceModle>)source).Select((Func<ToolFunctionSourceModle, IDataSourceHost>)((ToolFunctionSourceModle p) => p)).ToList();
	}

	internal static void Init()
	{
		if (!isInit)
		{
			isInit = true;
			Commands[0].SetAction(() =>
			{
				SigRun();
			});
			Commands[1].SetAction(() =>
			{
				MutiRun();
			});
			Commands[2].SetAction(() =>
			{
				StopRun();
			});
			_userManagedAction = () =>
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Expected Obj, but got Unknown
				LoginWindow val = new LoginWindow();
				val.SetUserStatus(false);
				((Window)(object)val).Topmost = true;
				((Window)(object)val).ShowDialog();
			};
			_userLoginAction = () =>
			{
				//IL_0001: Unknown result type (might be due to invalid IL or missing references)
				//IL_0007: Expected Obj, but got Unknown
				LoginWindow val = new LoginWindow();
				val.SetUserStatus(true);
				((Window)(object)val).Topmost = true;
				return ((Window)(object)val).ShowDialog();
			};
		}
	}

	private static void SolSet()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		SolutionManagerDialog val = new SolutionManagerDialog();
		((FrameworkElement)(object)val).DataContext = Program.MainData.SolutionData;
		if (((BaseWindow)(object)val).ShowDialog() ?? true)
		{
			Program.MainData.SolutionData.SaveSolInfo();
		}
	}

	public void LoadOldLightMoudle()
	{
		VmModule vmModule = VmSolution.Instance.Modules.FirstOrDefault((VmModule m) => m.ID == 14000);
		TitleMenueItemData titleMenueItemData = Items.OfType<TitleMenueItemData>().FirstOrDefault((TitleMenueItemData p) => p.Name == "Lang_System");
		if (titleMenueItemData != null)
		{
			ControllerSet = titleMenueItemData.Items.OfType<TitleMenueItemData>().FirstOrDefault((TitleMenueItemData p) => p.Name == "Lang_ControllerSet");
			if (ControllerSet != null)
			{
				TitleMenueItemData controllerSet = ControllerSet;
				bool flag = (ControllerSet.IsVisible = vmModule != null);
				controllerSet.IsDisplayInMenu = flag;
			}
		}
	}

	public static void DeleteOldLightModule()
	{
		VmModule oldLightMoudle = VmSolution.Instance.Modules.FirstOrDefault((VmModule m) => m.ID == 14000);
		if (oldLightMoudle == null)
		{
			return;
		}
		LightSourceManager.Instance.ModuleID = 14000;
		LightSourceManager.Instance.GetAuthority();
		LightSourceManager.Instance.GetData();
		ObservableCollection<object> itemsA = LightSourceManager.Instance.ItemsA;
		if (itemsA != null && itemsA.Count == 0)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_LogTips"), AppLocalizationService.GetLang("UpgradedControllerTips"), MessageButtons.NoButton, MessageIcons.Ok);
			if (ControllerSet != null)
			{
				ControllerSet.IsVisible = false;
			}
			System.Windows.Application.Current.Dispatcher?.Invoke(() =>
			{
				VmSolution.Instance.Modules.Remove(oldLightMoudle);
				ServerSDKManager.serverSDKManager.mModuleManager.CloseOneModule(oldLightMoudle.ID);
				oldLightMoudle = null;
			});
		}
	}
}
