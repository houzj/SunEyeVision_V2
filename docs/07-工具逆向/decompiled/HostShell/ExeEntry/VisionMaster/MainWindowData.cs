using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlUI;
using FrontendUI.Design;
using Hik.Xml;
using Mvvm.Commands;
using VM.AppData;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.User;
using VM.Utility;
using VM.Utility.SingleInstance;
using VMControls.Data;
using VMControls.Interface;
using VMControls.WPF.GlobalUI;
using VisionMaster.AppMainWindow;
using VisionMaster.Project;
using VisionMasterMain;
using VisionMasterMain.AppMainWindow;

namespace VisionMaster;

public class MainWindowData : NotifyPropertyChangedBase
{
	private AppData appData = null;

	private string MenuItems = "MenuItems.xml";

	private HikMessageBox ProxyCrashBox;

	private bool isDisplayBox = false;

	private AccessManager _AccessManager = AccessManager.GetInstance();

	private TitleMenueItemData menuSource;

	private object menuToolSource;

	private bool isWorkAreaLocking;

	private bool _IsOpenParams = false;

	private bool? isNotifyLock;

	private ProcedureRunData procRunPolicyData;

	private SolutionItemsData solutionData;

	private SoftWareData softwareData;

	private string maskMessage;

	private DelegateCommand openSolPathCommand = null;

	private WindowState mainWindowState = WindowState.Normal;

	private DelegateCommand openRunSetCommand = null;

	private bool isShowProgressMask;

	private double configZoomIndex;

	private IVmModule selectedModule;

	private static bool IsDogStateAlerted;

	private string _strDogState;

	private static bool IsNetUsedAlerted;

	private static bool IsMemeryUsedAlerted;

	private static int CPUState = 0;

	private static bool IsCPUUsedAlerted;

	private static bool IsRemainTimeAlerted;

	private string isShowSaveNotice = "1";

	private bool isLoading = false;

	public AccessManager AccessManager => _AccessManager;

	public TitleMenueItemData MenuSource => menuSource;

	public object MenuToolSource
	{
		get
		{
			if (menuToolSource == null)
			{
				List<IGrouping<string, TitleMenueItemData>> list = (from p in menuSource.GetAllChildren().OfType<TitleMenueItemData>()
					where p.ToolGroupItemInfo != null
					group p by p.ToolGroupItemInfo.groupName).ToList();
				list.Sort((IGrouping<string, TitleMenueItemData> p1, IGrouping<string, TitleMenueItemData> p2) => string.Compare(p1.Key, p2.Key));
				List<List<TitleMenueItemData>> list2 = list.Select((IGrouping<string, TitleMenueItemData> p) => p.ToList()).ToList();
				list2.ForEach((List<TitleMenueItemData> p) =>
				{
					p.Sort((TitleMenueItemData titleMenueItemData, TitleMenueItemData titleMenueItemData2) => (titleMenueItemData.ToolGroupItemInfo.Priority > titleMenueItemData2.ToolGroupItemInfo.Priority) ? 1 : (-1));
				});
				List<TitleMenueItemData> list3 = list2.SelectMany((List<TitleMenueItemData> p) => p.ToArray()).ToList();
				menuToolSource = list3;
			}
			return menuToolSource;
		}
	}

	public bool IsWorkAreaLocking
	{
		get
		{
			return isWorkAreaLocking;
		}
		set
		{
			if (isWorkAreaLocking != value)
			{
				isWorkAreaLocking = value;
				Ioc.Get<IUnRedoService>()?.SetLockStatus(value);
				LockProcess(value);
				OnPropertyChanged("IsWorkAreaLocking");
			}
		}
	}

	public bool IsOpenParams
	{
		get
		{
			return _IsOpenParams;
		}
		set
		{
			if (_IsOpenParams != value)
			{
				Ioc.Get<IUnRedoService>()?.SetOpenParamValue(value);
				_IsOpenParams = value;
				OnPropertyChanged("IsOpenParams");
			}
		}
	}

	public bool IsNotifyLock
	{
		get
		{
			if (!isNotifyLock.HasValue)
			{
				isNotifyLock = bool.Parse(ConfigurationHelper.GetConfig("IsNotifyLock"));
			}
			return isNotifyLock.Value;
		}
		internal set
		{
			if (isNotifyLock != value)
			{
				isNotifyLock = value;
				ConfigurationHelper.SaveConfig("IsNotifyLock", isNotifyLock.ToString());
			}
		}
	}

	public int NumOfDefLockModeModulesCount
	{
		get
		{
			int result = 128;
			try
			{
				result = int.Parse(ConfigurationHelper.GetConfig("NumOfDefLockModeModulesCount"));
			}
			catch
			{
			}
			return result;
		}
	}

	public int PromptNumOfLockModeModulesCount
	{
		get
		{
			int result = 80;
			try
			{
				result = int.Parse(ConfigurationHelper.GetConfig("PromptNumOfLockModeModulesCount"));
			}
			catch
			{
			}
			return result;
		}
	}

	public ProcedureRunData ProcRunPolicyData
	{
		get
		{
			if (procRunPolicyData == null)
			{
				procRunPolicyData = new ProcedureRunData();
			}
			return procRunPolicyData;
		}
	}

	public SolutionItemsData SolutionData
	{
		get
		{
			if (solutionData == null)
			{
				solutionData = new SolutionItemsData();
				solutionData.LoadSolInfo();
			}
			return solutionData;
		}
	}

	public SoftWareData SoftWareData
	{
		get
		{
			if (softwareData == null)
			{
				softwareData = new SoftWareData();
				softwareData.LoadSoftWareInfo();
			}
			return softwareData;
		}
	}

	public bool IsNormalMode { get; set; }

	public string MaskMessage
	{
		get
		{
			return maskMessage;
		}
		set
		{
			if (maskMessage != value)
			{
				maskMessage = value;
				OnPropertyChanged("MaskMessage");
			}
		}
	}

	public string SolutionPath => AppLocalizationService.GetLang("Lang_Path") + "：" + VmSolution.Instance.SolutionPath;

	public string SolutionName
	{
		get
		{
			if (string.IsNullOrEmpty(VmSolution.Instance.SolutionPath))
			{
				return "";
			}
			return Path.GetFileNameWithoutExtension(SolutionPath);
		}
	}

	public ICommand OpenSolPathCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = openSolPathCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)OpenSolutionPathCmdExcute);
				DelegateCommand val3 = val2;
				openSolPathCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand OpenRunSetCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = openRunSetCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)OpenRunSetCommandExcute);
				DelegateCommand val3 = val2;
				openRunSetCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public bool IsShowProgressMask
	{
		get
		{
			return isShowProgressMask;
		}
		set
		{
			if (isShowProgressMask != value)
			{
				isShowProgressMask = value;
				OnPropertyChanged("IsShowProgressMask");
			}
		}
	}

	public double ConfigZoomIndex
	{
		get
		{
			return configZoomIndex;
		}
		set
		{
			if (configZoomIndex != value)
			{
				configZoomIndex = value;
				OnPropertyChanged("ConfigZoomIndex");
			}
		}
	}

	public IVmModule SelectedModule
	{
		get
		{
			return selectedModule;
		}
		set
		{
			if (value != selectedModule)
			{
				selectedModule = value;
				OnPropertyChanged("SelectedModule");
			}
		}
	}

	public string StrEncryptType => SystemConfigManager.Instance.StrEncryptType;

	public DogStateEnum DogState => VmSolution.DogState;

	public int SoftLanguage => VmSolution.SoftLanguage;

	public string StrDongleSerialNo => VmSolution.StrDongleSerialNo;

	public bool IsUnlimitOfCamera => VmSolution.CameraNum >= 10000;

	public bool IsUnlimitOfRemainTime => VmSolution.RemainTime >= 1000000;

	public int CameraNum => VmSolution.CameraNum;

	public int RemainTime => VmSolution.RemainTime;

	public int RemainTimeDay => VmSolution.RemainTime / 24;

	public int RemainTimeHour => VmSolution.RemainTime % 24;

	public string StrDogState
	{
		get
		{
			if (SystemConfigManager.Instance.IsSoftEncrypt)
			{
				if (SystemConfigManager.Instance.GetAuthTime() == 0)
				{
					if (SystemConfigManager.Instance._stLicenseTime.enLicenseType == EncryptDefine.AUTH_LOCALHOST_LICENSE_TYPE.AUTH_LOCALHOST_LICENSE_PERPETUAL)
					{
						_strDogState = AppLocalizationService.GetLang("Lang_Permanent");
					}
					else if (SystemConfigManager.Instance._stLicenseTime.enLicenseType == EncryptDefine.AUTH_LOCALHOST_LICENSE_TYPE.AUTH_LOCALHOST_LICENSE_LIMIT_VALUE)
					{
						_strDogState = "";
						if (SystemConfigManager.Instance._stLicenseTime.nAvailableDay != 0)
						{
							_strDogState = _strDogState + Convert.ToString(SystemConfigManager.Instance._stLicenseTime.nAvailableDay) + AppLocalizationService.GetLang("Day");
						}
						if (SystemConfigManager.Instance._stLicenseTime.nAvailableCount != 0)
						{
							_strDogState = _strDogState + Convert.ToString(SystemConfigManager.Instance._stLicenseTime.nAvailableCount) + AppLocalizationService.GetLang("次");
						}
					}
				}
				else
				{
					_strDogState = "Error!";
				}
				return _strDogState;
			}
			return VmSolution.StrDogState;
		}
	}

	public bool IsSoftEncrypt => SystemConfigManager.Instance.IsSoftEncrypt;

	public int NetUsed => VmSolution.NetUsed;

	public int MemeryUsed => VmSolution.MemeryUsed;

	public int CPUUsed => VmSolution.CPUUsed;

	public bool IsAlert => RemainTimeAlert || MemeryAlert || NetAlert || CPUAlert || (VmSolution.DogState != DogStateEnum.NORMAL && VmSolution.DogState != DogStateEnum.Default);

	public bool NetAlert => VmSolution.NetUsed >= 99;

	public bool MemeryAlert => VmSolution.MemeryUsed > 90;

	public bool CPUAlert => VmSolution.CPUUsed > 90;

	public bool RemainTimeAlert => VmSolution.RemainTime < 72 && VmSolution.RemainTime > 0;

	public ImageSource LogoImage
	{
		get
		{
			BitmapImage bitmapImage = new BitmapImage();
			bitmapImage.BeginInit();
			bitmapImage.UriSource = new Uri(AppDomain.CurrentDomain.BaseDirectory + "Resources\\logo_VM.png", UriKind.RelativeOrAbsolute);
			bitmapImage.EndInit();
			return bitmapImage;
		}
	}

	public bool IsHideLogo
	{
		get
		{
			if (appData != null)
			{
				return !appData.IsHideLogo;
			}
			return false;
		}
	}

	public MainWindowData()
	{
		LogHelper.Info("MainWindowData-------begin");
		VmSolution.RestoreDefaultDisplay += VmSolution_RestoreDisplay;
		VmSolution.OnSolutionLoadEndCallBack += VmSolution_LoadEnd;
		VmSolution.OnSolutionLoadBeginCallBack += VmSolution_LoadBegin;
		VmSolution.OnGlobalScriptReportCallBack += VmSolution_OnGlobalScriptReportCallBack;
		VmSolution.OnExecuteAfterLoad += VmSolution_OnExecuteAfterLoad;
		VmSolution.OnReportModuleData += VmSolution_OnReportModuleData;
		VmSolution.OnProxyCrashNoticeCallBack += VmSolution_OnProxyCrashNotice;
		VmSolution.OnServerStatusCallBack += VmSolution_OnServerStatusCallBack;
		VmSolution.OnProcedureUpgradeBeginCallBack += VmSolution_OnSolutionUpgradeBeginCallBack;
		VmSolution.OnProcedureUpgradeEndCallBack += VmSolution_OnSolutionUpgradeEndCallBack;
		VmSolution.OnGroupUpgradeBeginCallBack += VmSolution_OnSolutionUpgradeBeginCallBack;
		VmSolution.OnGroupUpgradeEndCallBack += VmSolution_OnSolutionUpgradeEndCallBack;
		VmSolution.OnProcessImportEndCallBack += VmSolution_OnProcessImportEndCallBack;
		VmSolution.Instance.PropertyChanged += SourcePropertyChanged;
		LogHelper.Info("MainWindowData-------事件注册完毕");
		appData = AppDataFactory.Instance(true);
		CheckIsNeutral();
		InitComTrigger();
		LogHelper.Info("MainWindowData-------end");
	}

	private void VmSolution_OnExecuteAfterLoad(object sender, ObjectEventArgs args)
	{
		if (!(bool)args.Param)
		{
			return;
		}
		if (SystemConfigManager.Instance.IsSlientExecute)
		{
			Task.Run(() =>
			{
				LogHelper.Info("开始执行静默");
				VmSolution.Instance.SilentExecute(SystemConfigManager.Instance.IsAllModuSlientExecute);
				LogHelper.Info("结束执行静默");
			});
		}
		else
		{
			((IVmSolution)VmSolution.Instance).SetServerReadyEvent();
		}
	}

	private void VmSolution_OnReportModuleData(object sender, ObjectEventArgs args)
	{
		object param;
		if (!((param = args.Param) is ImvsSdkDefine.IMVS_ALGORITHM_INIT_RESULT_INFO))
		{
			return;
		}
		ImvsSdkDefine.IMVS_ALGORITHM_INIT_RESULT_INFO algResultInfou = (ImvsSdkDefine.IMVS_ALGORITHM_INIT_RESULT_INFO)param;
		if (true)
		{
			Application.Current.Dispatcher.BeginInvoke((Action)(() =>
			{
				string text = "";
				uint num = 0u;
				int num2 = -1;
				num2 = (int)algResultInfou.nModuleID;
				num = algResultInfou.nType;
				text = algResultInfou.strTransData;
				UpdateErrorInfoToUILog(num2, num, text);
			}));
		}
	}

	private void UpdateErrorInfoToUILog(int moduleID, uint reportType, string reportData)
	{
		switch (reportType)
		{
		case 3758100480u:
			if (reportData != "0")
			{
				ModuleAlgInitFail(moduleID, reportData);
			}
			break;
		case 3758100481u:
			ShowMessageToUILog(moduleID, reportData);
			break;
		case 1536u:
			UpdateCommonErrorInfoToUILog(moduleID, reportType, reportData);
			break;
		case 2684354560u:
			if (reportData != "0")
			{
				UpdateRecivedData(moduleID, reportData);
			}
			break;
		}
	}

	private void UpdateCommonErrorInfoToUILog(int moduleID, uint reportType, string reportData)
	{
		try
		{
			int result = 0;
			string text = "";
			string text2 = "";
			string arg = "";
			bool result2 = true;
			string[] array = reportData.Split('\v');
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Split('\t');
				if (array2.Length == 2)
				{
					if (array2[0].Equals("ErrorCode", StringComparison.OrdinalIgnoreCase))
					{
						int.TryParse(array2[1], out result);
					}
					else if (array2[0].Equals("Index", StringComparison.OrdinalIgnoreCase))
					{
						text2 = array2[1];
					}
					else if (array2[0].Equals("AdditionalInfo", StringComparison.OrdinalIgnoreCase))
					{
						arg = array2[1];
					}
					else if (array2[0].Equals("Level", StringComparison.OrdinalIgnoreCase))
					{
						text = array2[1];
					}
					else if (array2[0].Equals("Persistence", StringComparison.OrdinalIgnoreCase) && !bool.TryParse(array2[1], out result2))
					{
						result2 = true;
					}
				}
			}
			string text3 = "";
			text3 = (string.IsNullOrEmpty(text2) ? $"{moduleID},{ErrorCodeInfo.GetErrorInfo(result)},{arg}" : string.Format("{0}, {1} {2} {3}, {4}", moduleID, AppLocalizationService.GetLang("Lang_Article"), text2, AppLocalizationService.GetLang("Lang_Data"), ErrorCodeInfo.GetErrorInfo(result)));
			if (!string.IsNullOrEmpty(text))
			{
				if (text.Equals("Info", StringComparison.OrdinalIgnoreCase))
				{
					UILogManager.logCreate("信息", text3, false, result2);
				}
				else if (text.Equals("Warn", StringComparison.OrdinalIgnoreCase))
				{
					UILogManager.logCreate("警告", text3, false, result2);
				}
				else if (text.Equals("Error", StringComparison.OrdinalIgnoreCase))
				{
					UILogManager.logCreate("错误", text3, false, result2);
				}
			}
			else
			{
				UILogManager.logCreate("错误", text3, false, result2);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_Err", ex.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	private void ShowMessageToUILog(int moduleID, string reportData)
	{
		try
		{
			if (reportData.IndexOf('_') > -1)
			{
				int.TryParse(reportData.Substring(0, reportData.IndexOf('_')), out var result);
				int.TryParse(reportData.Substring(reportData.IndexOf('_') + 1), out var result2);
				string text = string.Format("{0}, {1} {2} {3}, {4}", moduleID, AppLocalizationService.GetLang("Lang_Article"), result, AppLocalizationService.GetLang("Lang_Data"), ErrorCodeInfo.GetErrorInfo(result2));
				UILogManager.logCreate("错误", text, false, true);
			}
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_Err", ex.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	private void ModuleAlgInitFail(int moduID, string errCode)
	{
		try
		{
			string text = "";
			int num = Convert.ToInt32(errCode.Trim());
			object lang = AppLocalizationService.GetLang("Lang_ModuleAlgInitFail");
			text = ((lang == null) ? (AppLocalizationService.GetLang("模块初始化失败") + " :" + num.ToString("X") + " " + ErrorCodeInfo.GetErrorInfo(num)) : (lang.ToString() + num.ToString("X") + "(" + ErrorCodeInfo.GetErrorInfo(num) + ")"));
			UILogManager.logCreate("错误", text, false, true);
			HikMessageBox.Show("Lang_Err", text, MessageButtons.Normal, MessageIcons.Error);
		}
		catch (Exception ex)
		{
			HikMessageBox.Show("Lang_Err", ex.Message, MessageButtons.Normal, MessageIcons.Error);
		}
	}

	private void UpdateRecivedData(int moduID, string reportData)
	{
		string[] array = reportData.Split('\r');
		if (array.Length != 3)
		{
			return;
		}
		string s = array[0];
		string s2 = array[1];
		string data = array[2];
		if (!int.TryParse(s, out var processId) || !int.TryParse(s2, out var result) || (processId == -1 && result == -1))
		{
			return;
		}
		VmProcedure vmProcedure = null;
		if (processId == -1)
		{
			VmModule moduleByID = VmSolution.Instance.GetModuleByID((uint)result);
			if (moduleByID != null)
			{
				vmProcedure = moduleByID.Parent as VmProcedure;
			}
		}
		else
		{
			vmProcedure = (from a in VmSolution.Instance.Modules.OfType<VmProcedure>()
				where a.ID == (uint)processId
				select a).FirstOrDefault();
		}
		if (vmProcedure != null && ((IVmProcedure)vmProcedure).CalibrationData != null)
		{
			vmProcedure.UpdateResult?.Invoke(data);
		}
	}

	public void DoClose()
	{
		VmSolution.RestoreDefaultDisplay -= VmSolution_RestoreDisplay;
		VmSolution.OnSolutionLoadEndCallBack -= VmSolution_LoadEnd;
		VmSolution.OnSolutionLoadBeginCallBack -= VmSolution_LoadBegin;
		VmSolution.OnGlobalScriptReportCallBack -= VmSolution_OnGlobalScriptReportCallBack;
		VmSolution.OnProxyCrashNoticeCallBack -= VmSolution_OnProxyCrashNotice;
		VmSolution.OnReportModuleData -= VmSolution_OnReportModuleData;
		VmSolution.OnServerStatusCallBack -= VmSolution_OnServerStatusCallBack;
		VmSolution.OnProcessImportEndCallBack -= VmSolution_OnProcessImportEndCallBack;
	}

	private void VmSolution_RestoreDisplay(object sender, EventArgs e)
	{
		VmSolution vmSolution = (e as ObjectEventArgs).Param as VmSolution;
		vmSolution.PropertyChanged -= SourcePropertyChanged;
		VmSolution.Instance.PropertyChanged -= SourcePropertyChanged;
		VmSolution.Instance.PropertyChanged += SourcePropertyChanged;
	}

	private void Instance_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		throw new NotImplementedException();
	}

	private void VmSolution_LoadBegin(object sender, EventArgs e)
	{
		if (GlobalCommunicationMgr.Instance.IsGet)
		{
			GlobalCommunicationMgr.Instance.CloseComm();
			GlobalCommunicationMgr.Instance.IsGet = false;
		}
		TitleMenueItemData.ResetResource();
	}

	private void VmSolution_LoadEnd(object sender, EndEventArgs e)
	{
		if (e != null && e.ErrorCode == 0)
		{
			if (VmSolution.Instance.GetModuleCount() >= NumOfDefLockModeModulesCount)
			{
				IsWorkAreaLocking = true;
			}
			else if (VmSolution.Instance.GetModuleCount() >= PromptNumOfLockModeModulesCount && VmSolution.Instance.GetModuleCount() < NumOfDefLockModeModulesCount)
			{
				Application.Current.Dispatcher.BeginInvoke((Action)(() =>
				{
					//IL_0014: Unknown result type (might be due to invalid IL or missing references)
					//IL_001a: Expected Obj, but got Unknown
					if (!Program.MainData.IsNotifyLock)
					{
						LockDlg val = new LockDlg(false);
						((BaseWindow)(object)val).ShowDialogDelay(2000u);
						IsNotifyLock = val.IsNotifyLock;
						if (((Window)(object)val).DialogResult ?? false)
						{
							IsWorkAreaLocking = true;
						}
						else
						{
							IsWorkAreaLocking = false;
						}
					}
				}));
			}
			else
			{
				IsWorkAreaLocking = false;
			}
			MenuSource.LoadOldLightMoudle();
		}
		ProjectManager.UpdateRecentlySolutions(VmSolution.Instance.SolutionPath);
		UILogManager.logCreate("信息", AppLocalizationService.GetLang("加载方案，路径：") + " " + VmSolution.Instance.SolutionPath, true, true);
		if (SoftWareData.IsAutoLoadLast)
		{
			SoftWareData.SetAutoLoadSol(isSaveConfig: true);
		}
		OnPropertyChanged("SolutionPath");
		OnPropertyChanged("SolutionName");
	}

	private void VmSolution_OnGlobalScriptReportCallBack(object sender, ObjectEventArgs args)
	{
		TitleMenueItemData.VmSolution_OnGlobalSciptReportData(sender, args);
	}

	private void VmSolution_OnProcessImportEndCallBack(object sender, EndEventArgs args)
	{
		if (!((IVmSolution)VmSolution.Instance).IsIgnoreGlobalModule)
		{
			if (GlobalCommunicationMgr.Instance.IsGet)
			{
				GlobalCommunicationMgr.Instance.ClearAll();
				GlobalCommunicationMgr.Instance.IsGet = false;
			}
			TitleMenueItemData.ResetResource();
		}
	}

	private void SourcePropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged(e.PropertyName);
		SystemResourceAlert(e.PropertyName);
		if (e.PropertyName == "SoftLanguage")
		{
			LogHelper.Info("----加密狗触发切换语言---start");
			DoChangeLanguage(VmSolution.SoftLanguage);
			LogHelper.Info("----加密狗触发切换语言---end");
		}
	}

	private void DoChangeLanguage(int softLanguage)
	{
		switch (softLanguage)
		{
		case 0:
		{
			ItemData itemData2 = menuSource?.items.FirstOrDefault((ItemData p) => p.name == "Lang_Help")?.items.FirstOrDefault((ItemData p) => p.name == "Lang_Language");
			if (itemData2 != null && itemData2.IsVisible)
			{
				itemData2.IsVisible = false;
			}
			string text = "zh-cn";
			if (AppLocalizationService.GetLangParm() != text)
			{
				AppLocalizationService.ChangeLanguage(text);
			}
			break;
		}
		case 1:
		{
			ItemData itemData = menuSource?.items.FirstOrDefault((ItemData p) => p.name == "Lang_Help")?.items.FirstOrDefault((ItemData p) => p.name == "Lang_Language");
			if (itemData != null && !itemData.IsVisible)
			{
				itemData.IsVisible = true;
			}
			break;
		}
		}
	}

	private void VmSolution_OnProxyCrashNotice(object sender, ObjectEventArgs args)
	{
		int status = (int)args.Param;
		Application.Current.Dispatcher.BeginInvoke((Action)(() =>
		{
			ProxyCrashBox = HikMessageBox.CreateMessageBox(AppLocalizationService.GetLang("ProxyCrash"), MessageIcons.Warning);
			if (status == 0)
			{
				try
				{
					TitleMenueItemData.ResetResource();
					VmSolution.Instance.Modules.Clear();
					Program.MainData.OnPropertyChanged("SolutionPath");
					Program.MainData.OnPropertyChanged("SolutionName");
				}
				catch (Exception ex)
				{
					LogHelper.objLog.Error(ex.Message);
				}
				if (ProxyCrashBox != null)
				{
					ProxyCrashBox.ShowDialog();
					ProxyCrashBox = null;
				}
			}
			else if (status == 1)
			{
				VmSolution.OnSolutionLoadBeginCallBack += ProxyCrash_LoadBegin;
				if (ProxyCrashBox != null)
				{
					ProxyCrashBox.ShowDialog();
					ProxyCrashBox = null;
				}
			}
		}));
	}

	private void VmSolution_OnServerSeizedCallBack(object sender, ObjectEventArgs args)
	{
		Application.Current.Dispatcher.BeginInvoke((Action)(() =>
		{
			if (!isDisplayBox)
			{
				isDisplayBox = true;
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.DestoryHandle(1);
				HikMessageBox.Show("Lang_ServerSeized", MessageIcons.Error, isTranslate: true);
				Environment.Exit(0);
			}
		}));
	}

	private void VmSolution_OnServerStatusCallBack(object sender, ObjectEventArgs args)
	{
		uint status = (uint)args.Param;
		Application.Current.Dispatcher.BeginInvoke((Action)(() =>
		{
			if (status != 0 && !isDisplayBox)
			{
				isDisplayBox = true;
				HikMessageBox.Show("Lang_ServerNoExist", MessageIcons.Error, isTranslate: true);
				Environment.Exit(0);
			}
		}));
	}

	private void ProxyCrash_LoadBegin(object sender, EventArgs e)
	{
		DoProxyCrash();
	}

	private void DoProxyCrash()
	{
		Application.Current.Dispatcher.Invoke(() =>
		{
			VmSolution.OnSolutionLoadBeginCallBack -= ProxyCrash_LoadBegin;
			if (ProxyCrashBox != null)
			{
				ProxyCrashBox.Close();
				ProxyCrashBox = null;
			}
		});
	}

	private void LockProcess(bool isLock)
	{
		TitleMenueItemData titleMenueItemData = menuSource.Items.OfType<TitleMenueItemData>().FirstOrDefault((TitleMenueItemData p) => p.Name == "Lang_File");
		if (titleMenueItemData != null)
		{
			TitleMenueItemData titleMenueItemData2 = titleMenueItemData.Items.OfType<TitleMenueItemData>().FirstOrDefault((TitleMenueItemData p) => p.Name == "Lang_ImportProcess");
			if (titleMenueItemData2 != null)
			{
				titleMenueItemData2.IsEnabled = !isLock;
			}
		}
	}

	private void OpenSolutionPathCmdExcute()
	{
		if (!RemoteOperate.IsRemoteMode && !string.IsNullOrEmpty(SolutionName))
		{
			string path = VmSolution.Instance.SolutionPath.Replace("\\" + SolutionName + "." + AppFileExtension.Solution, "");
			if (!Directory.Exists(path))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("方案路径不存在!"), MessageIcons.Error);
				return;
			}
			if (!File.Exists(VmSolution.Instance.SolutionPath))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("方案不存在!"), MessageIcons.Error);
				return;
			}
			string text = "\"" + VmSolution.Instance.SolutionPath + "\"";
			Process.Start("Explorer", "/select," + text);
		}
	}

	internal void RecoverMainWindowState(Window window)
	{
		if (window.WindowState != mainWindowState)
		{
			window.WindowState = mainWindowState;
		}
	}

	private void OpenRunSetCommandExcute()
	{
		Window window = FrontendWindowData.Instance.InitRunWindowFromDesignData();
		if (window != null)
		{
			window.Resources.MergedDictionaries.Add((ResourceDictionary)(object)LocalizationResourceDictionary.Instance);
			window.ShowInTaskbar = true;
			window.Show();
			Window mainWindow = Application.Current.MainWindow;
			MainWindow val = (MainWindow)(object)((mainWindow is MainWindow) ? mainWindow : null);
			mainWindowState = ((Window)(object)val).WindowState;
			Ioc.Get<IApplicationOperation>()?.ReplaceMainWindow(window);
			((FrameworkElement)(object)val).DataContext = null;
			((BaseWindow)(object)val).Close();
			IsOpenParams = false;
		}
	}

	internal void PreLoad()
	{
		try
		{
			LogHelper.Info("PreLoad-------初始化TitleMenueItemData--Start");
			menuSource = new TitleMenueItemData();
			LogHelper.Info("PreLoad-------TitleMenueItemData--构造结束");
			menuSource.loadXml("Resources//" + MenuItems);
			ImvsSdkDefine.IMVS_DONGLE_INFO pstDongleInfo = default;
			if (ServerSDKManager.serverSDKManager.mSolutionManager.MakeServerReportDongleInfo(ref pstDongleInfo) == 0)
			{
				DoChangeLanguage(pstDongleInfo.nSoftLanguage);
			}
			else
			{
				DoChangeLanguage(VmSolution.SoftLanguage);
			}
			menuSource.LangPropertyChanged += Refresh;
			LogHelper.Info("PreLoad-------初始化TitleMenueItemData--end");
			ToolBoxData.LoadData();
		}
		catch (Exception ex)
		{
			VmException vmException = GetVmException(ex);
			if (vmException != null)
			{
				LogHelper.objLog.Error("重新启动失败:" + vmException.errorMessage);
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(vmException.errorCode));
			}
			else
			{
				HikMessageBox.Show(ex.Message);
				LogHelper.Error("主界面信息加载失败:" + ex.Message);
			}
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

	public void Refresh(object sender, EventArgs e)
	{
		string defaultNameSpace = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "VisionMaster.AppMainWindow";
		try
		{
			menuSource.loadXml("Resources//" + MenuItems);
			BaseNode.defaultNameSpace = defaultNameSpace;
			MenuSource.OnPropertyChanged("Items");
			menuToolSource = null;
			OnPropertyChanged("MenuToolSource");
			ToolBoxData.GetInstance().Reload();
			FunctionCameraManager.Execute("RefreshResultView");
			OnPropertyChanged("SolutionPath");
			if (SystemConfigManager.Instance.IsSoftEncrypt)
			{
				SystemConfigManager.Instance.StrEncryptType = AppLocalizationService.GetLang("软件期限");
			}
			else if (RemoteOperate.IsRemoteMode)
			{
				SystemConfigManager.Instance.StrEncryptType = AppLocalizationService.GetLang("设备型号");
			}
			else
			{
				SystemConfigManager.Instance.StrEncryptType = AppLocalizationService.GetLang("加密狗");
			}
			OnPropertyChanged("StrEncryptType");
			OnPropertyChanged("StrDogState");
			CodeManager.GetInstance().RefreshTitleShowUserName();
			List<VmProcedure> list = VmSolution.Instance.Modules.OfType<VmProcedure>().ToList();
			if (list == null)
			{
				return;
			}
			foreach (VmProcedure item in list)
			{
				item.OnPropertyChanged("WorkState");
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("刷新界面显示失败" + ex.Message);
		}
		finally
		{
			BaseNode.defaultNameSpace = defaultNameSpace;
		}
	}

	public void RefreshRecentSolution()
	{
		menuSource.Items.OfType<TitleMenueItemData>().FirstOrDefault((TitleMenueItemData p) => p.Name == "Lang_File")?.Items.OfType<TitleMenueItemData>().FirstOrDefault((TitleMenueItemData p) => p.Name == "Lang_OpenLastSol")?.RefreshRecentSolution();
	}

	private void CheckIsNeutral()
	{
		if (appData == null)
		{
			return;
		}
		if (appData.IsNeutral)
		{
			if (!RemoteOperate.IsRemoteMode)
			{
				MenuItems = "NeutralMenuItems.xml";
			}
			else
			{
				MenuItems = "NeutralMenuItemsLinux.xml";
			}
		}
		else if (!RemoteOperate.IsRemoteMode)
		{
			MenuItems = "MenuItems.xml";
		}
		else
		{
			MenuItems = "MenuItemsLinux.xml";
		}
	}

	private void SystemResourceAlert(string propertyName)
	{
		switch (propertyName)
		{
		case "MemeryUsed":
			OnPropertyChanged("IsAlert");
			OnPropertyChanged("MemeryAlert");
			if (!IsMemeryUsedAlerted && MemeryAlert && !IsNormalMode)
			{
				IsMemeryUsedAlerted = true;
				LogHelper.objLog.Warn("内存占用高！" + MemeryUsed);
				Application.Current.Dispatcher.BeginInvoke((Action)(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("内存占用高") + " !\r\n" + AppLocalizationService.GetLang("AttentionOfComputer"), MessageIcons.Warning);
				}));
			}
			break;
		case "NetUsed":
			OnPropertyChanged("IsAlert");
			OnPropertyChanged("NetAlert");
			if (!IsNetUsedAlerted && NetAlert && !IsNormalMode)
			{
				IsNetUsedAlerted = true;
				LogHelper.objLog.Warn("网络使用率高！" + NetUsed);
				Application.Current.Dispatcher.BeginInvoke((Action)(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_AttentionOfNet"), MessageIcons.Warning);
				}));
			}
			break;
		case "CPUUsed":
			OnPropertyChanged("IsAlert");
			OnPropertyChanged("CPUAlert");
			if (IsCPUUsedAlerted || IsNormalMode)
			{
				break;
			}
			if (CPUAlert)
			{
				if (CPUState == 0)
				{
					CPUState = 1;
				}
				else if (CPUState == 1)
				{
					CPUState = 0;
					IsCPUUsedAlerted = true;
					LogHelper.objLog.Warn("CPU占用高！" + CPUUsed);
					Application.Current.Dispatcher.BeginInvoke((Action)(() =>
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("CPU占用高") + " !\r\n" + AppLocalizationService.GetLang("AttentionOfComputerCPU"), MessageIcons.Warning);
					}));
				}
			}
			else
			{
				CPUState = 0;
			}
			break;
		case "DogState":
			OnPropertyChanged("IsAlert");
			if (!IsDogStateAlerted && DogState != DogStateEnum.NORMAL)
			{
				if (SystemConfigManager.Instance.IsSoftEncrypt)
				{
					LogHelper.objLog.Warn("软加密异常！");
					Application.Current.Dispatcher.BeginInvoke((Action)(() =>
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("软加密") + AppLocalizationService.GetLang("异常") + " !", MessageIcons.Warning);
					}));
				}
				else
				{
					LogHelper.objLog.Warn("加密狗异常！");
					Application.Current.Dispatcher.BeginInvoke((Action)(() =>
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("加密狗") + AppLocalizationService.GetLang("异常") + " !", MessageIcons.Warning);
					}));
				}
			}
			OnPropertyChanged("IsAlert");
			break;
		case "CameraNum":
			OnPropertyChanged("IsUnlimitOfCamera");
			OnPropertyChanged("IsAlert");
			break;
		case "RemainTime":
			OnPropertyChanged("IsUnlimitOfRemainTime");
			OnPropertyChanged("RemainTimeAlert");
			OnPropertyChanged("IsAlert");
			OnPropertyChanged("RemainTimeDay");
			OnPropertyChanged("RemainTimeHour");
			if (!IsRemainTimeAlerted && RemainTimeAlert)
			{
				IsRemainTimeAlerted = true;
				LogHelper.objLog.Warn("加密狗即将过期！ ");
				Application.Current.Dispatcher.BeginInvoke((Action)(() =>
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Dongle_about_dated") + " !", MessageIcons.Warning);
				}));
			}
			break;
		}
	}

	private void VmSolution_OnSolutionUpgradeBeginCallBack(object sender, ObjectEventArgs args)
	{
		Application.Current.Dispatcher.BeginInvoke((Action)(() =>
		{
			string text = (string)args.Param;
			MaskMessage = text;
			IsShowProgressMask = true;
		}));
	}

	private void VmSolution_OnSolutionUpgradeEndCallBack(object sender, ObjectEventArgs args)
	{
		Application.Current.Dispatcher.BeginInvoke((Action)(() =>
		{
			IsShowProgressMask = false;
		}));
	}

	private void InitComTrigger()
	{
		VmSolution.OnCommunicationRecvCallBackEvent += VmSolution_OnCommunicationRecvCallBackEvent;
		isShowSaveNotice = ConfigurationHelper.GetConfig("IsShowSaveNotice");
	}

	private void VmSolution_OnCommunicationRecvCallBackEvent(ImvsSdkDefine.IMVS_COMMU_REPORT_DATA_INFO reportDataInfo)
	{
		if (reportDataInfo.nLen < 2 || reportDataInfo.pData == IntPtr.Zero)
		{
			return;
		}
		byte[] array = new byte[reportDataInfo.nLen];
		Marshal.Copy(reportDataInfo.pData, array, 0, reportDataInfo.nLen);
		string text = Encoding.UTF8.GetString(array, 2, array.Length - 2);
		SolItem solItem = SolutionData.GetSolItem(text);
		LogHelper.Debug($"VmSolutionCommad={text},solcommItemIsNull={solItem == null}");
		if (solItem == null)
		{
			return;
		}
		bool loadSolutionResult = true;
		try
		{
			if (isLoading)
			{
				return;
			}
			isLoading = true;
			Task.Run(() =>
			{
				while (!((IVmSolution)VmSolution.Instance).IsLoading && loadSolutionResult)
				{
					Thread.Sleep(1);
				}
				isLoading = false;
			});
			if (!((IVmSolution)VmSolution.Instance).IsLoading && !ProjectManager.GetInstance().LoadSolution(solItem.SolPath, isGuid: false, (solItem.SolPassword == null) ? "" : solItem.SolPassword, null, isRemoteMode: false, isFromAppStart: false, isFromAppArgs: false, isShowSaveNotice == "1"))
			{
				loadSolutionResult = false;
			}
		}
		catch (Exception ex)
		{
			VmException ex2 = null;
			if (ex is VmException || ex.InnerException is VmException)
			{
				loadSolutionResult = false;
				ex2 = ex as VmException;
				if (ex2 == null)
				{
					ex2 = ex.InnerException as VmException;
				}
				LogHelper.Error(ex2.errorMessage);
			}
			else
			{
				HikMessageBox.Show(ex.Message, MessageIcons.Error);
				LogHelper.Error(ex.Message);
			}
		}
	}
}
