using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using Hik.Util;
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using TaskScheduler;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.User;
using VM.Utility;
using VM.Utility.SingleInstance;
using VM.WatchDog;
using VMControls.Interface;
using VMControls.RenderInterface;
using VisionMaster.Project;
using VisionMasterMain;

namespace VisionMaster.AppMainWindow;

public class SoftWareData : NotifyPropertyChangedBaseNode
{
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct CloseSoftSet
	{
		public bool closeSoftwaraEnable;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
		public string closeSoftwaraCode;
	}

	private const string Role_Admin_Key = "管理员";

	private const string Role_Tech_Key = "技术员";

	private const string Role_Operator_Key = "操作员";

	public static Dictionary<string, string> accessMap = new Dictionary<string, string>
	{
		{ "管理员", "ef84e765e027ce1984c2be0aabaea66afb9e66eb" },
		{ "技术员", "1c6e56e9e2c157e9650a92509aaf3ba7c4cf750b" },
		{ "操作员", "0c7c09a07e129b8bb76dc7547589c875ced22804" }
	};

	public AlgorithmTabRoot ConfigRoot = null;

	private const string _xmlPath = "Resources//SoftWareItems.xml";

	private IEnumerable _advanceSetting;

	private bool _IsRunBackground = true;

	private bool _isOperatorEnabled = true;

	private bool _autoStartEnable = false;

	private bool _closeSoftwarePasswordEnabled = false;

	private string _closeSoftwarePassword = "";

	private bool _hideGuideWindow = false;

	private bool _isMaxWhenStartUp = false;

	private bool _isFullScreenWhenStartUp = false;

	private bool _isFrontend = true;

	private bool _isMaximizedWhenFrontendIndependently = false;

	private bool _isAutoLoad = false;

	private bool _isAutoLoadLast = false;

	private bool _isRemeberServerMode = false;

	private bool _isLocalServer = false;

	private bool _isRemoteServer = false;

	private bool _MVDWarmupGpuEnable = false;

	private DelegateCommand _PathSelectCommand = null;

	private string _solPath = "";

	private string _solPassword = "";

	public bool _isLargeImageSave;

	public string _largeFilePath;

	private DelegateCommand _ClickCommand = null;

	private int _runState = 0;

	private int _loadAccessIndex = 0;

	private double _delayTime = 0.0;

	private bool _ifSlientExecute = false;

	private int _maxModuleCount = 256;

	private double _unRedoStep = 0.0;

	private double _moduleSpan = 40.0;

	private bool _isModifyTriggerRun = false;

	private bool _isModelTriggerRun = true;

	private int _procedureDelayTime = 0;

	private bool _isHighQualityImage = false;

	private int _shapeNumLimit = 10000;

	private ObservableCollection<AccessInfo> _accessItems;

	private string _selectedAccessValue = string.Empty;

	private bool _frontendVisible = false;

	private List<string> runStateOptions = new List<string>
	{
		AppLocalizationService.GetLang("静默执行"),
		AppLocalizationService.GetLang("Lang_MutiRun"),
		AppLocalizationService.GetLang("不执行")
	};

	private string AUTOSTARTTASK = "VisionMasterAutoStart";

	private DelegateCommand closeSoftewarePasswordCommand = null;

	public static CloseSoftSet closeSoftSet = default;

	public IEnumerable AdvanceSetting
	{
		get
		{
			return _advanceSetting;
		}
		set
		{
			if (_advanceSetting != value)
			{
				_advanceSetting = value;
				OnPropertyChanged("AdvanceSetting");
			}
		}
	}

	public bool IsRunBackground
	{
		get
		{
			return _IsRunBackground;
		}
		set
		{
			if (_IsRunBackground != value)
			{
				_IsRunBackground = value;
				IsStopRunBack = !value;
				OnPropertyChanged("IsRunBackground");
			}
		}
	}

	public bool IsStopRunBack { get; set; }

	public bool IsOperatorEnabled
	{
		get
		{
			return _isOperatorEnabled;
		}
		set
		{
			if (_isOperatorEnabled != value)
			{
				_isOperatorEnabled = value;
				OnPropertyChanged("IsOperatorEnabled");
			}
		}
	}

	public bool AutoStartEnable
	{
		get
		{
			return _autoStartEnable;
		}
		set
		{
			if (_autoStartEnable != value)
			{
				_autoStartEnable = value;
				OnPropertyChanged("AutoStartEnable");
			}
		}
	}

	public bool CloseSoftwarePasswordEnabled
	{
		get
		{
			return _closeSoftwarePasswordEnabled;
		}
		set
		{
			if (_closeSoftwarePasswordEnabled != value)
			{
				_closeSoftwarePasswordEnabled = value;
				OnPropertyChanged("CloseSoftwarePasswordEnabled");
			}
		}
	}

	public string CloseSoftwarePassword
	{
		get
		{
			return _closeSoftwarePassword;
		}
		set
		{
			if (_closeSoftwarePassword != value)
			{
				_closeSoftwarePassword = value;
				OnPropertyChanged("CloseSoftwarePassword");
			}
		}
	}

	public bool HideGuideWindow
	{
		get
		{
			return _hideGuideWindow;
		}
		set
		{
			if (_hideGuideWindow != value)
			{
				_hideGuideWindow = value;
				OnPropertyChanged("HideGuideWindow");
			}
		}
	}

	public bool IsMaxWhenStartUp
	{
		get
		{
			return _isMaxWhenStartUp;
		}
		set
		{
			if (_isMaxWhenStartUp != value)
			{
				_isMaxWhenStartUp = value;
				OnPropertyChanged("IsMaxWhenStartUp");
			}
		}
	}

	public bool IsFullScreenWhenStartUp
	{
		get
		{
			return _isFullScreenWhenStartUp;
		}
		set
		{
			if (_isFullScreenWhenStartUp != value)
			{
				_isFullScreenWhenStartUp = value;
				OnPropertyChanged("IsFullScreenWhenStartUp");
			}
		}
	}

	public bool IsFronted
	{
		get
		{
			return _isFrontend;
		}
		set
		{
			if (_isFrontend != value)
			{
				_isFrontend = value;
				OnPropertyChanged("IsFronted");
			}
		}
	}

	public bool IsMaximizedWhenFrontendIndependently
	{
		get
		{
			return _isMaximizedWhenFrontendIndependently;
		}
		set
		{
			if (_isMaximizedWhenFrontendIndependently != value)
			{
				_isMaximizedWhenFrontendIndependently = value;
				OnPropertyChanged("IsMaximizedWhenFrontendIndependently");
			}
		}
	}

	public bool IsAutoLoad
	{
		get
		{
			return _isAutoLoad;
		}
		set
		{
			if (_isAutoLoad != value)
			{
				_isAutoLoad = value;
				if (_isAutoLoadLast && _isAutoLoad)
				{
					IsAutoLoadLast = false;
				}
				OnPropertyChanged("IsAutoLoad");
			}
		}
	}

	public bool IsAutoLoadLast
	{
		get
		{
			return _isAutoLoadLast;
		}
		set
		{
			if (_isAutoLoadLast == value)
			{
				return;
			}
			_isAutoLoadLast = value;
			if (_isAutoLoadLast)
			{
				GetServerSol();
				ProjectManager.UpdateRecentlySolutions(VmSolution.Instance.SolutionPath);
				if (_isAutoLoad)
				{
					IsAutoLoad = false;
				}
			}
			OnPropertyChanged("IsAutoLoadLast");
		}
	}

	public bool IsRemeberServerMode
	{
		get
		{
			return _isRemeberServerMode;
		}
		set
		{
			if (_isRemeberServerMode != value)
			{
				_isRemeberServerMode = value;
				OnPropertyChanged("IsRemeberServerMode");
			}
		}
	}

	public bool IsLocalServer
	{
		get
		{
			return _isLocalServer;
		}
		set
		{
			if (_isLocalServer != value)
			{
				_isLocalServer = value;
				OnPropertyChanged("IsLocalServer");
			}
		}
	}

	public bool IsRemoteServer
	{
		get
		{
			return _isRemoteServer;
		}
		set
		{
			if (_isRemoteServer != value)
			{
				_isRemoteServer = value;
				OnPropertyChanged("IsRemoteServer");
			}
		}
	}

	public bool MVDWarmupGpuEnable
	{
		get
		{
			return _MVDWarmupGpuEnable;
		}
		set
		{
			if (_MVDWarmupGpuEnable != value)
			{
				_MVDWarmupGpuEnable = value;
				OnPropertyChanged("MVDWarmupGpuEnable");
			}
		}
	}

	public ICommand PathSelectCommand
	{
		get
		{
			if (_PathSelectCommand == null)
			{
				_PathSelectCommand = new DelegateCommand(PathSelectCmdExecute);
			}
			return _PathSelectCommand;
		}
	}

	public string SolPath
	{
		get
		{
			return _solPath;
		}
		set
		{
			_solPath = value;
			OnPropertyChanged("SolPath");
		}
	}

	public string SolPassword
	{
		get
		{
			return _solPassword;
		}
		set
		{
			_solPassword = value;
			OnPropertyChanged("SolPassword");
		}
	}

	public bool IsLargeImageSave
	{
		get
		{
			return _isLargeImageSave;
		}
		set
		{
			if (_isLargeImageSave != value)
			{
				if (_isLargeImageSave != value)
				{
					_isLargeImageSave = value;
				}
				if (value && string.IsNullOrEmpty(LargeFilePath))
				{
					LargeFilePath = AppDomain.CurrentDomain.BaseDirectory;
				}
				OnPropertyChanged("IsLargeImageSave");
			}
		}
	}

	public string LargeFilePath
	{
		get
		{
			return _largeFilePath;
		}
		set
		{
			if (_largeFilePath != value)
			{
				if (_largeFilePath != value)
				{
					_largeFilePath = value;
				}
				OnPropertyChanged("LargeFilePath");
			}
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand(ClickCommandCmdExecute);
			}
			return _ClickCommand;
		}
	}

	public int RunState
	{
		get
		{
			return _runState;
		}
		set
		{
			if (_runState != value)
			{
				_runState = value;
				OnPropertyChanged("RunState");
			}
		}
	}

	public int LoadAccessIndex
	{
		get
		{
			return _loadAccessIndex;
		}
		set
		{
			if (_loadAccessIndex != value)
			{
				_loadAccessIndex = value;
				OnPropertyChanged("LoadAccessIndex");
			}
		}
	}

	public double DelayTime
	{
		get
		{
			return _delayTime;
		}
		set
		{
			if (_delayTime != value)
			{
				_delayTime = value;
				OnPropertyChanged("DelayTime");
			}
		}
	}

	public bool IfSlientExecute
	{
		get
		{
			return _ifSlientExecute;
		}
		set
		{
			if (_ifSlientExecute != value)
			{
				_ifSlientExecute = value;
				OnPropertyChanged("IfSlientExecute");
			}
		}
	}

	public int MaxModuleCount
	{
		get
		{
			return _maxModuleCount;
		}
		set
		{
			if (_maxModuleCount != value)
			{
				_maxModuleCount = value;
				OnPropertyChanged("MaxModuleCount");
			}
		}
	}

	public int ModuleCountRangeMax { get; set; } = 1024;

	public double UnRedoStep
	{
		get
		{
			return _unRedoStep;
		}
		set
		{
			if (_unRedoStep != value)
			{
				_unRedoStep = value;
				OnPropertyChanged("UnRedoStep");
			}
		}
	}

	public double ModuleSpan
	{
		get
		{
			return _moduleSpan;
		}
		set
		{
			if (_moduleSpan != value)
			{
				_moduleSpan = value;
				OnPropertyChanged("ModuleSpan");
			}
		}
	}

	public bool IsModifyTriggerRun
	{
		get
		{
			return _isModifyTriggerRun;
		}
		set
		{
			if (_isModifyTriggerRun != value)
			{
				_isModifyTriggerRun = value;
				OnPropertyChanged("IsModifyTriggerRun");
			}
		}
	}

	public bool IsModelTriggerRun
	{
		get
		{
			return _isModelTriggerRun;
		}
		set
		{
			if (_isModelTriggerRun != value)
			{
				_isModelTriggerRun = value;
				OnPropertyChanged("IsModelTriggerRun");
			}
		}
	}

	public int ProcedureDelayTime
	{
		get
		{
			return _procedureDelayTime;
		}
		set
		{
			if (_procedureDelayTime != value)
			{
				_procedureDelayTime = value;
				OnPropertyChanged("ProcedureDelayTime");
			}
		}
	}

	public bool IsHighQualityImage
	{
		get
		{
			return _isHighQualityImage;
		}
		set
		{
			if (_isHighQualityImage != value)
			{
				_isHighQualityImage = value;
				OnPropertyChanged("IsHighQualityImage");
			}
		}
	}

	public int ShapeNumLimit
	{
		get
		{
			return _shapeNumLimit;
		}
		set
		{
			if (_shapeNumLimit != value && value >= 1000)
			{
				_shapeNumLimit = value;
				OnPropertyChanged("ShapeNumLimit");
			}
		}
	}

	public ObservableCollection<AccessInfo> AccessItems
	{
		get
		{
			if (_accessItems == null)
			{
				_accessItems = new ObservableCollection<AccessInfo>();
			}
			return _accessItems;
		}
		set
		{
			if (value != _accessItems)
			{
				_accessItems = value;
				OnPropertyChanged("AccessItems");
			}
		}
	}

	public string SelectedAccessValue
	{
		get
		{
			return _selectedAccessValue;
		}
		set
		{
			if (_selectedAccessValue != value)
			{
				_selectedAccessValue = value;
				OnPropertyChanged("SelectedAccessValue");
			}
		}
	}

	public bool FrontendVisible
	{
		get
		{
			return _frontendVisible;
		}
		set
		{
			if (_frontendVisible != value)
			{
				_frontendVisible = value;
				OnPropertyChanged("FrontendVisible");
			}
		}
	}

	public List<string> RunStateOptions => runStateOptions;

	public ICommand CloseSoftewarePasswordCommand
	{
		get
		{
			if (closeSoftewarePasswordCommand == null)
			{
				closeSoftewarePasswordCommand = new DelegateCommand(CloseSoftewarePassword);
			}
			return closeSoftewarePasswordCommand;
		}
	}

	public bool GetIsRestart()
	{
		foreach (object item in AdvanceSetting)
		{
			if (item is IConfirm confirm && confirm.ModifyReStart())
			{
				return true;
			}
		}
		return false;
	}

	public void LoadXmlConfigData()
	{
		if (ConfigRoot == null)
		{
			ConfigRoot = new AlgorithmTabRoot();
			ConfigRoot.loadXml("Resources//SoftWareItems.xml");
			if (AdvanceSetting == null)
			{
				AdvanceSetting = GetItemSource("SoftWateSetting", "AdvancedSetting");
			}
			{
				foreach (object item in AdvanceSetting)
				{
					(item as NameValueItem).InniterBusiness();
				}
				return;
			}
		}
		ConfigRoot.DoIniter();
	}

	private IEnumerable GetItemSource(string tabName, string categoryName)
	{
		Tab tab = ConfigRoot.Tabs.OfType<Tab>().FirstOrDefault((Tab p) => p.NameA == tabName);
		if (tab != null)
		{
			Category category = tab.Categorys.OfType<Category>().FirstOrDefault((Category p) => p.Name == categoryName);
			if (category != null)
			{
				return category.ItemsA;
			}
		}
		return null;
	}

	private void ExecuteSetParam()
	{
		string text = null;
		foreach (object item in AdvanceSetting)
		{
			if (item is IConfirm confirm)
			{
				confirm.ConfirmData();
				if (!string.IsNullOrEmpty(confirm.MessageText))
				{
					text = text + confirm.MessageText + "\n";
					confirm.MessageText = null;
				}
			}
		}
		if (!string.IsNullOrEmpty(text))
		{
			HikMessageBox.Show(text, MessageIcons.Error);
		}
	}

	private void ClickCommandCmdExecute()
	{
		DoOpenFile();
	}

	private void SetBigImageSave()
	{
		try
		{
			ServerSDKManager.serverSDKManager.mSolutionManager.SetSaveImageModuBigFile(0u, LargeFilePath, IsLargeImageSave);
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex2.errorCode), MessageIcons.Error);
				LogHelper.Error("SetBigImageSave is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
		}
	}

	public void SaveBigImageSet()
	{
		SetBigImageSave();
	}

	public void LoadSolParam()
	{
		try
		{
			string strPath = "";
			bool bIsEnableSetBigFile = false;
			ServerSDKManager.serverSDKManager.mSolutionManager.GetSaveImageModuBigFile(0u, ref strPath, ref bIsEnableSetBigFile);
			if (bIsEnableSetBigFile)
			{
				LargeFilePath = strPath;
			}
			IsLargeImageSave = bIsEnableSetBigFile;
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				HikMessageBox.Show(ex2.errorMessage, MessageIcons.Error);
				LogHelper.Error("SetBigImageSave is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
		}
	}

	private void DoOpenFile()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Invalid comparison between Unknown and I4
		CommonOpenFileDialog val = new CommonOpenFileDialog();
		val.IsFolderPicker = true;
		val.Multiselect = false;
		((CommonFileDialog)val).AllowPropertyEditing = true;
		CommonFileDialogResult val2 = ((CommonFileDialog)val).ShowDialog();
		try
		{
			if ((int)val2 != 2)
			{
				LargeFilePath = ((CommonFileDialog)val).FileName.Trim();
			}
		}
		catch (Exception)
		{
		}
	}

	private void PathSelectCmdExecute()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Solution Files (*." + AppFileExtension.Solution + ")|*." + AppFileExtension.Solution
		};
		string path = ConfigurationHelper.INIGetStringValue("OpenSol");
		Apps.UIHelper.Extern.UIHelper.CheckInitDirectory(openFileDialog, path);
		if (openFileDialog.ShowDialog() == true)
		{
			SolPath = openFileDialog.FileName;
			if (File.Exists(SolPath))
			{
				ConfigurationHelper.INISetStringValue("OpenSol", Path.GetDirectoryName(SolPath));
			}
		}
	}

	public void InitAccess()
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Invalid comparison between Unknown and I4
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f0: Invalid comparison between Unknown and I4
		AccessItems.Clear();
		AccessUser loginUser = CodeManager.GetInstance().GetLoginUser();
		if ((int)loginUser == 1)
		{
			AccessItems.Add(new AccessInfo
			{
				UserName = "管理员",
				UserDisPlayName = AppLocalizationService.GetLang("Lang_Role_Admin")
			});
			bool flag = false;
			string text = "";
			CodeManager.GetInstance().GetTechCode(ref flag, ref text);
			if (flag)
			{
				AccessItems.Add(new AccessInfo
				{
					UserName = "技术员",
					UserDisPlayName = AppLocalizationService.GetLang("Lang_Role_Tech")
				});
			}
			flag = false;
			CodeManager.GetInstance().GetOperatorCode(ref flag, ref text);
			if (flag)
			{
				AccessItems.Add(new AccessInfo
				{
					UserName = "操作员",
					UserDisPlayName = AppLocalizationService.GetLang("Lang_Role_Operator")
				});
			}
		}
		else if ((int)loginUser == 2)
		{
			AccessItems.Add(new AccessInfo
			{
				UserName = "技术员",
				UserDisPlayName = AppLocalizationService.GetLang("Lang_Role_Tech")
			});
			bool flag2 = false;
			string text2 = "";
			CodeManager.GetInstance().GetOperatorCode(ref flag2, ref text2);
			if (flag2)
			{
				AccessItems.Add(new AccessInfo
				{
					UserName = "操作员",
					UserDisPlayName = AppLocalizationService.GetLang("Lang_Role_Operator")
				});
			}
		}
		IsOperatorEnabled = Convert.ToInt32(ConfigurationHelper.GetConfig("IsOperatorEnabled")) == 1;
	}

	public void LoadSoftWareInfo()
	{
		InitAccess();
		UnRedoStep = Convert.ToDouble(ConfigurationHelper.GetConfig("UnRedoStackCount"));
		ModuleSpan = Convert.ToDouble(ConfigurationHelper.GetConfig("ModuleSpanCount"));
		AutoStartEnable = Convert.ToBoolean(ConfigurationHelper.GetConfig("IsAutoLaunch"));
		DelayTime = Convert.ToDouble(ConfigurationHelper.GetConfig("AutoLaunchDelayTime"));
		CloseSoftwarePasswordEnabled = Convert.ToInt32(ConfigurationHelper.GetConfig("CloseSoftwarePasswordEnabled")) == 1;
		HideGuideWindow = !Convert.ToBoolean(ConfigurationHelper.GetConfig("ShowEnable"));
		IsMaxWhenStartUp = Convert.ToBoolean(ConfigurationHelper.GetConfig("IsMaximizedForVM"));
		IsFullScreenWhenStartUp = Convert.ToBoolean(ConfigurationHelper.GetConfig("IsFullScreenForVM"));
		IsFronted = Convert.ToBoolean(ConfigurationHelper.GetConfig("FrontendFirst"));
		IsMaximizedWhenFrontendIndependently = bool.Parse(ConfigurationHelper.GetConfig("IsMaximizedWhenFrontendIndependently"));
		string tmpAcess = ConfigurationHelper.GetConfig("AutoLaunchAccess");
		if (!string.IsNullOrEmpty(tmpAcess))
		{
			SelectedAccessValue = accessMap.FirstOrDefault((KeyValuePair<string, string> q) => q.Value == tmpAcess).Key;
		}
		GetAutoLoadSolState();
		IsRunBackground = Convert.ToInt32(ConfigurationHelper.GetConfig("IsRunBackground")) == 1;
		IsRemeberServerMode = bool.Parse(ConfigurationHelper.GetConfig("IsRemeberServerChecked"));
		IsRemoteServer = RemoteOperate.IsRemoteMode;
		IsLocalServer = !IsRemoteServer;
		runStateOptions = new List<string>
		{
			AppLocalizationService.GetLang("静默执行"),
			AppLocalizationService.GetLang("Lang_MutiRun"),
			AppLocalizationService.GetLang("不执行")
		};
		LoadXmlConfigData();
	}

	public static void SetAutoLaunchAccess()
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Invalid comparison between Unknown and I4
		//IL_0103: Unknown result type (might be due to invalid IL or missing references)
		//IL_010a: Expected Obj, but got Unknown
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a2: Expected Obj, but got Unknown
		try
		{
			ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO iMVS_AUTO_LOAD_SOLUTION_INFO = default;
			bool flag = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetAutoLoadSolution().Value.nIfAutoLoad == 1;
			string tmpAcess = ConfigurationHelper.GetConfig("AutoLaunchAccess");
			string key = accessMap.FirstOrDefault((KeyValuePair<string, string> q) => q.Value == tmpAcess).Key;
			if (string.IsNullOrWhiteSpace(key))
			{
				return;
			}
			bool flag2 = false;
			string accessAdministatorCode = "";
			if (!flag || (int)CodeManager.GetInstance().GetLoginUser() <= 0)
			{
				return;
			}
			switch (key)
			{
			case "操作员":
				CodeManager.GetInstance().GetOperatorCode(ref flag2, ref accessAdministatorCode);
				if (!flag2)
				{
					CodeManager.GetInstance().GetAdminCode(ref flag2, ref accessAdministatorCode);
					AccesssCodeCheck val2 = new AccesssCodeCheck();
					val2.AccessAdministatorCode = accessAdministatorCode;
					val2.ShowInfoTips();
					if (true == ((BaseWindow)(object)val2).ShowDialog())
					{
						AccessManagerHelper.SetLoginUser((AccessUser)1);
						break;
					}
					DeInitExit();
				}
				AccessManagerHelper.SetLoginUser((AccessUser)3);
				break;
			case "管理员":
				AccessManagerHelper.SetLoginUser((AccessUser)1);
				break;
			case "技术员":
				CodeManager.GetInstance().GetTechCode(ref flag2, ref accessAdministatorCode);
				if (!flag2)
				{
					CodeManager.GetInstance().GetAdminCode(ref flag2, ref accessAdministatorCode);
					AccesssCodeCheck val = new AccesssCodeCheck();
					val.AccessAdministatorCode = accessAdministatorCode;
					val.ShowInfoTips();
					if (true == ((BaseWindow)(object)val).ShowDialog())
					{
						AccessManagerHelper.SetLoginUser((AccessUser)1);
						break;
					}
					DeInitExit();
				}
				AccessManagerHelper.SetLoginUser((AccessUser)2);
				break;
			}
		}
		catch
		{
		}
	}

	private static void DeInitExit()
	{
		try
		{
			VmSolution.Instance.Dispose(isTakeOver: false);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error("销毁句柄失败:" + ex.Message);
		}
		finally
		{
			Environment.Exit(0);
		}
	}

	public void SaveSoftWareInfo()
	{
		ConfigurationHelper.SaveConfig("IsAutoLaunch", AutoStartEnable.ToString());
		ConfigurationHelper.SaveConfig("AutoLaunchDelayTime", DelayTime.ToString());
		ConfigurationHelper.SaveConfig("ShowEnable", (!HideGuideWindow).ToString());
		ConfigurationHelper.SaveConfig("IsMaximizedForVM", IsMaxWhenStartUp.ToString());
		ConfigurationHelper.SaveConfig("IsFullScreenForVM", IsFullScreenWhenStartUp.ToString());
		ConfigurationHelper.SaveConfig("FrontendFirst", IsFronted.ToString());
		ConfigurationHelper.SaveConfig("IsMaximizedWhenFrontendIndependently", IsMaximizedWhenFrontendIndependently.ToString());
		ConfigurationHelper.SaveConfig("IsAutoLoadPath", IsAutoLoad.ToString());
		ConfigurationHelper.SaveConfig("IsRemeberServerChecked", IsRemeberServerMode.ToString());
		ConfigurationHelper.SaveConfig("AutoLoadPath", SolPath.ToString());
		ConfigurationHelper.SaveConfig("AutoLoadSolPassword", SolPassword.ToString().String2Base64());
		string value = accessMap.FirstOrDefault((KeyValuePair<string, string> q) => q.Key == SelectedAccessValue).Value;
		ConfigurationHelper.SaveConfig("AutoLaunchAccess", value);
		string defaultValue;
		if (RunState == 1)
		{
			defaultValue = AppLocalizationService.GetLang("Lang_MutiRun");
		}
		else
		{
			defaultValue = ((RunState == 0) ? AppLocalizationService.GetLang("非连续运行") : AppLocalizationService.GetLang("不执行"));
		}
		ConfigurationHelper.SaveConfig("AutoLaunchRunState", defaultValue);
		ConfigurationHelper.SaveConfig("IsRunBackground", IsRunBackground ? "1" : "0");
		ConfigurationHelper.SaveConfig("UnRedoStackCount", UnRedoStep.ToString());
		ConfigurationHelper.SaveConfig("ModuleSpanCount", ModuleSpan.ToString());
		ConfigurationHelper.SaveConfig("CloseSoftwarePasswordEnabled", CloseSoftwarePasswordEnabled ? "1" : "0");
		SaveBigImageSet();
		SetUnRedoStep(UnRedoStep);
		SetAutoLaunch(AutoStartEnable, (int)DelayTime);
		SetAutoLoadSol(isSaveConfig: true);
		ExecuteSetParam();
	}

	private void SetUnRedoStep(double step)
	{
		Ioc.Get<IUnRedoService>()?.UpdateUnRedoStep(step);
	}

	public void SaveAccessInfo()
	{
		ConfigurationHelper.SaveConfig("IsOperatorEnabled", IsOperatorEnabled ? "1" : "0");
	}

	public int GetAutoLoadSolState()
	{
		ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO iMVS_AUTO_LOAD_SOLUTION_INFO = default;
		try
		{
			iMVS_AUTO_LOAD_SOLUTION_INFO = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetAutoLoadSolution().Value;
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				string errorInfo = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
				HikMessageBox.Show(errorInfo, MessageIcons.Error);
				LogHelper.Error("GetAutoLoadSolution is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
			return 0;
		}
		IsAutoLoad = iMVS_AUTO_LOAD_SOLUTION_INFO.nIfAutoLoad == 1;
		IsAutoLoadLast = iMVS_AUTO_LOAD_SOLUTION_INFO.nIfAutoLoad == 2;
		RefreshAccessVisible();
		RunState = ((iMVS_AUTO_LOAD_SOLUTION_INFO.nExecuteStatus == 2) ? 1 : ((iMVS_AUTO_LOAD_SOLUTION_INFO.nExecuteStatus != 1) ? 2 : 0));
		SolPassword = VM.PlatformSDKCS.StringUtil.UTF8GetString(iMVS_AUTO_LOAD_SOLUTION_INFO.strPassWord);
		SolPath = VM.PlatformSDKCS.StringUtil.UTF8GetString(iMVS_AUTO_LOAD_SOLUTION_INFO.strSolutionPath);
		return 0;
	}

	public void GetServerSol()
	{
		ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO iMVS_AUTO_LOAD_SOLUTION_INFO = default;
		try
		{
			iMVS_AUTO_LOAD_SOLUTION_INFO = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetAutoLoadSolution().Value;
			SolPassword = VM.PlatformSDKCS.StringUtil.UTF8GetString(iMVS_AUTO_LOAD_SOLUTION_INFO.strPassWord);
			SolPath = VM.PlatformSDKCS.StringUtil.UTF8GetString(iMVS_AUTO_LOAD_SOLUTION_INFO.strSolutionPath);
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				string errorInfo = ErrorCodeInfo.GetErrorInfo(ex2.errorCode);
				HikMessageBox.Show(errorInfo, MessageIcons.Error);
				LogHelper.Error("GetServerSol is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
		}
	}

	private int GetMaxModuleNum()
	{
		try
		{
			uint maxModuleNum = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetMaxModuleNum();
			MaxModuleCount = Convert.ToInt32(maxModuleNum);
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				HikMessageBox.Show(ex2.errorMessage, MessageIcons.Error);
				LogHelper.Error("GetMaxModuleCount is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
			return 0;
		}
		return 0;
	}

	private int SetMaxModuleNum()
	{
		try
		{
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetMaxModuleNum(Convert.ToUInt32(MaxModuleCount));
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				HikMessageBox.Show(ex2.errorMessage, MessageIcons.Error);
				LogHelper.Error("SetMaxModuleNum is error:" + ex2.errorMessage);
			}
			LogHelper.Error(ex);
			return 0;
		}
		return 0;
	}

	private void GetMVDWarmupGpuEnable(ref string strValue)
	{
		try
		{
			int num = -536870657;
			uint nModuleID = 0u;
			string strName = "MVDWarmupGpuEnable";
			num = ServerSDKManager.serverSDKManager.mSolutionManager.GetGlobalConfig(nModuleID, strName, ref strValue);
			if (num != 0)
			{
				VmException ex = new VmException(num);
				throw ex;
			}
		}
		catch (Exception ex2)
		{
			if (ex2 is VmException ex3)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex3.errorCode), MessageIcons.Error);
				LogHelper.Error("GetMVDWarmupGpuEnable is error:" + ex3.errorMessage);
			}
			else
			{
				LogHelper.Error(ex2.Message + ex2.StackTrace);
			}
		}
	}

	private void SetMVDWarmupGpuEnable()
	{
		try
		{
			int num = -536870657;
			uint nModuleID = 0u;
			string strName = "MVDWarmupGpuEnable";
			num = ServerSDKManager.serverSDKManager.mSolutionManager.SetGlobalConfig(nModuleID, strName, MVDWarmupGpuEnable.ToString().ToLower());
			if (num != 0)
			{
				VmException ex = new VmException(num);
				throw ex;
			}
		}
		catch (Exception ex2)
		{
			if (ex2 is VmException ex3)
			{
				HikMessageBox.Show(ErrorCodeInfo.GetErrorInfo(ex3.errorCode), MessageIcons.Error);
				LogHelper.Error("MVDWarmupGpuEnable is error:" + ex3.errorMessage);
			}
			else
			{
				LogHelper.Error(ex2.Message + ex2.StackTrace);
			}
		}
	}

	private void SetProcedureDelayTime()
	{
		try
		{
			VmSolution.SetProcedureDelayTime((uint)ProcedureDelayTime);
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				LogHelper.Error(ErrorCodeInfo.GetErrorInfo(ex2.errorCode) + "Error Code = " + ex2.errorCode);
			}
			else
			{
				LogHelper.Error(ex.Message + ex.StackTrace);
			}
		}
	}

	private void SetIsHighQualityImage()
	{
		GlobalRenderVariable.IsHighQualityImage = IsHighQualityImage;
	}

	private void SetShapeNumberLimit()
	{
		GlobalRenderVariable.ShapeRenderingMaxNumber = ShapeNumLimit;
	}

	public static void StartSetParamAboutRender()
	{
		try
		{
			int procedureDelayTime = Convert.ToInt32(ConfigurationHelper.GetConfig("ProcedureDelayTime"));
			VmSolution.SetProcedureDelayTime((uint)procedureDelayTime);
		}
		catch (Exception ex)
		{
			if (ex is VmException ex2)
			{
				LogHelper.Error(ErrorCodeInfo.GetErrorInfo(ex2.errorCode) + "Error Code = " + ex2.errorCode);
			}
			else
			{
				LogHelper.Error(ex.Message + ex.StackTrace);
			}
		}
		GlobalRenderVariable.IsHighQualityImage = Convert.ToBoolean(ConfigurationHelper.GetConfig("IsHighQualityImage"));
		GlobalRenderVariable.ShapeRenderingMaxNumber = Convert.ToInt32(ConfigurationHelper.GetConfig("ShapeNumberLimit"));
	}

	public static void EnableWatchDog()
	{
		try
		{
			if (Convert.ToBoolean(ConfigurationHelper.GetConfig("IsEnableWatchDog")))
			{
				WatchDogService.Instance.Register((MonitorOperator)0, new RecordOperator[1], 8000);
				WatchDogService.Instance.Start();
			}
		}
		catch (Exception value)
		{
			Console.WriteLine(value);
			throw;
		}
	}

	private void RefreshAccessVisible()
	{
	}

	public void SetAutoLoadSol(bool isSaveConfig = false)
	{
		ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO autoLoadSolution = new ImvsSdkDefine.IMVS_AUTO_LOAD_SOLUTION_INFO
		{
			nIfAutoLoad = (IsAutoLoad ? 1u : (IsAutoLoadLast ? 2u : 0u)),
			nExecuteStatus = ((RunState == 1) ? 2u : ((RunState != 2) ? 1u : 0u))
		};
		if (isSaveConfig)
		{
			ImvsSdkDefine.IMVS_PLATFORM_DETAILED_INFO iMVS_PLATFORM_DETAILED_INFO = default;
			iMVS_PLATFORM_DETAILED_INFO = ServerSDKManager.serverSDKManager.mVmInterfaceManager.GetPlatformDetailedInfo().Value;
			string str = Hik.Util.StringUtil.UTF8GetString(iMVS_PLATFORM_DETAILED_INFO.strSolutionPath);
			string str2 = Hik.Util.StringUtil.UTF8GetString(iMVS_PLATFORM_DETAILED_INFO.strPassWord);
			if (IsAutoLoadLast)
			{
				autoLoadSolution.strPassWord = VM.PlatformSDKCS.StringUtil.UTF8GetFixLenBytes(str2, 32);
				autoLoadSolution.strSolutionPath = VM.PlatformSDKCS.StringUtil.UTF8GetFixLenBytes(str, 780);
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetAutoLoadSolution(autoLoadSolution);
			}
			else
			{
				autoLoadSolution.strPassWord = VM.PlatformSDKCS.StringUtil.UTF8GetFixLenBytes(SolPassword, 32);
				autoLoadSolution.strSolutionPath = VM.PlatformSDKCS.StringUtil.UTF8GetFixLenBytes(SolPath, 780);
				ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetAutoLoadSolution(autoLoadSolution);
			}
		}
		else
		{
			autoLoadSolution.strPassWord = VM.PlatformSDKCS.StringUtil.UTF8GetFixLenBytes(SolPassword, 32);
			autoLoadSolution.strSolutionPath = VM.PlatformSDKCS.StringUtil.UTF8GetFixLenBytes(SolPath, 780);
			ServerSDKManager.serverSDKManager.mVmInterfaceManager.SetAutoLoadSolution(autoLoadSolution);
		}
	}

	private void SetParamModifyTrigger()
	{
		VmSolution.IsParamModifyExecute = IsModifyTriggerRun;
		VmSolution.IsModelModifyExecute = IsModelTriggerRun;
	}

	public static void StartSetParamModify()
	{
		VmSolution.IsParamModifyExecute = Convert.ToBoolean(ConfigurationHelper.GetConfig("ParamModifyTriggerRun"));
		VmSolution.IsModelModifyExecute = Convert.ToBoolean(ConfigurationHelper.GetConfig("ModelModifyTriggerRun"));
	}

	private void SetAutoLaunch(bool? bIsAutoLaunch, int nDelayTime)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0008: Expected Obj, but got Unknown
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Expected Obj, but got Unknown
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e4: Expected Obj, but got Unknown
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Expected Obj, but got Unknown
		TaskSchedulerClass val = new TaskSchedulerClass();
		val.Connect((object)null, (object)null, (object)null, (object)null);
		ITaskFolder folder = val.GetFolder("\\");
		try
		{
			while (((IEnumerable)folder.GetTasks(0)).Cast<IRegisteredTask>().Any((IRegisteredTask p) => p.Name == AUTOSTARTTASK))
			{
				folder.DeleteTask(AUTOSTARTTASK, 0);
			}
		}
		catch (Exception ex)
		{
			HikMessageBox.Show(ex.Message, MessageIcons.Error);
		}
		if (bIsAutoLaunch.Value)
		{
			TaskSchedulerClass val2 = new TaskSchedulerClass();
			val2.Connect((object)null, (object)null, (object)null, (object)null);
			ITaskFolder folder2 = val2.GetFolder("\\");
			ITaskDefinition val3 = val2.NewTask(0u);
			val3.RegistrationInfo.Author = "VisionMaster";
			val3.RegistrationInfo.Description = AppLocalizationService.GetLang("Lang_Startuptask");
			ILogonTrigger val4 = (ILogonTrigger)val3.Triggers.Create((_TASK_TRIGGER_TYPE2)9);
			val4.Delay = "PT" + nDelayTime + "S";
			val3.Principal.RunLevel = (_TASK_RUNLEVEL)1;
			try
			{
				IExecAction val5 = (IExecAction)val3.Actions.Create((_TASK_ACTION_TYPE)0);
				val5.Path = System.Windows.Forms.Application.ExecutablePath;
				val5.WorkingDirectory = Environment.CurrentDirectory;
				val3.Settings.ExecutionTimeLimit = "PT0S";
				val3.Settings.DisallowStartIfOnBatteries = false;
				val3.Settings.RunOnlyIfIdle = false;
				IRegisteredTask val6 = folder2.RegisterTaskDefinition(AUTOSTARTTASK, val3, 2, (object)null, (object)null, (_TASK_LOGON_TYPE)3, (object)"");
			}
			catch
			{
			}
		}
	}

	private void CloseSoftewarePassword()
	{
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected Obj, but got Unknown
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Expected Obj, but got Unknown
		if (!CloseSoftwarePasswordEnabled)
		{
			CloseSoftwarePasswordEnabled = true;
			LoadSoftwaraSet();
			AccesssCodeCheck val = new AccesssCodeCheck();
			bool bEnable = false;
			string code = "";
			GetCloseSoftwareCode(out bEnable, out code);
			val.AccessAdministatorCode = code;
			((Window)(object)val).Title = AppLocalizationService.GetLang("验证关闭软件密码");
			if (false != ((BaseWindow)(object)val).ShowDialog())
			{
				CloseSoftwarePasswordEnabled = false;
			}
			return;
		}
		CloseSoftwarePasswordEnabled = false;
		AccessCodeSet val2 = new AccessCodeSet();
		((Window)(object)val2).Title = AppLocalizationService.GetLang("设置关闭软件密码");
		if (false != ((BaseWindow)(object)val2).ShowDialog())
		{
			VM.Controls.DialogResult dialogResult = HikMessageBox.Show(AppLocalizationService.GetLang("Lang_EnableColsePassword"), AppLocalizationService.GetLang("Lang_WarningClosePassword"), MessageButtons.YesOrNo, MessageIcons.Warning, isTranslate: true);
			if (dialogResult == VM.Controls.DialogResult.Yes)
			{
				SetCloseSoftwareCode(bEnable: true, val2.AccessAdministatorCode);
				CloseSoftwarePasswordEnabled = true;
			}
		}
	}

	public static bool LoadSoftwaraSet()
	{
		if (ConfigurationHelper.GetConfig("CloseSoftwarePasswordEnabled") == "1")
		{
			byte[] array = Convert.FromBase64String(ConfigurationHelper.GetConfig("CloseSoftwarePassword"));
			if (array == null)
			{
				return false;
			}
			object obj = CodeManager.GetInstance().BytesToStruct(array, typeof(CloseSoftSet));
			if (obj == null)
			{
				return false;
			}
			closeSoftSet = (CloseSoftSet)obj;
		}
		return true;
	}

	public static bool SetCloseSoftwareCode(bool bEnable, string code)
	{
		closeSoftSet.closeSoftwaraEnable = bEnable;
		closeSoftSet.closeSoftwaraCode = code;
		byte[] inArray = CodeManager.GetInstance().StructTOBytes((object)closeSoftSet);
		string defaultValue = Convert.ToBase64String(inArray);
		ConfigurationHelper.SaveConfig("CloseSoftwarePassword", defaultValue);
		return true;
	}

	public static bool GetCloseSoftwareCode(out bool bEnable, out string code)
	{
		bEnable = closeSoftSet.closeSoftwaraEnable;
		code = closeSoftSet.closeSoftwaraCode;
		return true;
	}
}
