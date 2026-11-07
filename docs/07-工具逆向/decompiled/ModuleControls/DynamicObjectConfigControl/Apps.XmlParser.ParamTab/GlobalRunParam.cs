using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.ErrorCode;
using Apps.Localization;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.XmlData;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;
using VMControls.WPF.DataForUI;

namespace Apps.XmlParser.ParamTab;

public class GlobalRunParam : NameValueItem
{
	private const string GlobalParamNameKey = "GlobalParamNameKey";

	private const string GlobalParamIdKey = "GlobalParamIdKey";

	private ObservableCollection<GlobalParamConfigItem> _GlobalParamItems;

	private GlobalParamConfigItem _SelectedGlobalItem;

	private GlobalParamConfigData _GlobalData;

	private bool _IsMultiRun;

	private IDecorate _ImageDecorate;

	private ObservableCollection<DynamicOutputData> _DynamicOutputList;

	private DelegateCommand closingCommand = null;

	public ObservableCollection<GlobalParamConfigItem> GlobalParamItems
	{
		get
		{
			if (_GlobalParamItems == null)
			{
				_GlobalParamItems = new ObservableCollection<GlobalParamConfigItem>();
			}
			return _GlobalParamItems;
		}
		set
		{
			if (_GlobalParamItems != value)
			{
				_GlobalParamItems = value;
				OnPropertyChanged("GlobalParamItems");
			}
		}
	}

	public GlobalParamConfigItem SelectedGlobalItem
	{
		get
		{
			return _SelectedGlobalItem;
		}
		set
		{
			if (_SelectedGlobalItem != value)
			{
				_SelectedGlobalItem = value;
				OnPropertyChanged("SelectedGlobalItem");
			}
		}
	}

	public bool IsEmpty => GlobalParamItems == null || GlobalParamItems.Count == 0;

	public IDecorate ImageDecorate
	{
		get
		{
			return _ImageDecorate;
		}
		set
		{
			if (_ImageDecorate != value)
			{
				_ImageDecorate = value;
				OnPropertyChanged("ImageDecorate");
			}
		}
	}

	public List<string> GlobalOptions
	{
		get
		{
			List<GlobalParamConfigItem> list = GlobalParamItems?.Where((GlobalParamConfigItem p) => p.IsVisible).ToList();
			return (list != null) ? list.Select((GlobalParamConfigItem p) => p.Name).ToList() : null;
		}
	}

	public GlobalParamConfigData GlobalData
	{
		get
		{
			if (_GlobalData == null)
			{
				_GlobalData = new GlobalParamConfigData();
			}
			return _GlobalData;
		}
		set
		{
			if (_GlobalData != value)
			{
				_GlobalData = value;
			}
		}
	}

	public bool IsMultiRun
	{
		get
		{
			return _IsMultiRun;
		}
		set
		{
			if (_IsMultiRun != value)
			{
				_IsMultiRun = value;
				OnPropertyChanged("IsMultiRun");
			}
		}
	}

	public ObservableCollection<DynamicOutputData> DynamicOutputList
	{
		get
		{
			if (_DynamicOutputList == null)
			{
				_DynamicOutputList = new ObservableCollection<DynamicOutputData>();
			}
			return _DynamicOutputList;
		}
		set
		{
			if (_DynamicOutputList != value)
			{
				_DynamicOutputList = value;
				OnPropertyChanged("DynamicOutputList");
			}
		}
	}

	public IVmModule ModuleSource => VmSolution.Instance;

	public ICommand EditCommand { get; }

	public ICommand SelectCommand { get; }

	public ICommand MultiRunCommand { get; }

	public ICommand SigRunCommand { get; }

	public ICommand StopRunCommand { get; }

	public ICommand ConfirmCommand { get; }

	public ICommand ClosingCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = closingCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ClosingCommandExecute);
				DelegateCommand val3 = val2;
				closingCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public GlobalRunParam()
	{
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected Obj, but got Unknown
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Expected Obj, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected Obj, but got Unknown
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected Obj, but got Unknown
		IsMultiRun = ((IVMRun)VmSolution.Instance).ContinuousRunEnable;
		EditCommand = (ICommand)new DelegateCommand((Action)EditCommandExecute);
		SelectCommand = (ICommand)new DelegateCommand<string>((Action<string>)SelectCommandExecute);
		MultiRunCommand = (ICommand)new DelegateCommand((Action)MultiRun);
		SigRunCommand = (ICommand)new DelegateCommand((Action)SigRun);
		StopRunCommand = (ICommand)new DelegateCommand((Action)StopRun);
	}

	public void LoadData()
	{
		List<string> list = new List<string>();
		List<int> list2 = new List<int>();
		List<string> list3 = new List<string>();
		byte[] array = VmSolution.Instance.UserData["GlobalParamNameKey"];
		if (array != null)
		{
			list = StringUtil.UTF8GetString(array).Split(';').ToList();
		}
		byte[] array2 = VmSolution.Instance.UserData["GlobalParamIdKey"];
		if (array2 != null)
		{
			list2 = (from p in StringUtil.UTF8GetString(array2).Split(';')
				select int.Parse(p)).ToList();
		}
		int num = ((list.Count == list2.Count) ? list.Count : 0);
		if (num <= 0)
		{
			return;
		}
		GlobalParamItems.Clear();
		for (int num2 = 0; num2 < num; num2++)
		{
			GlobalParamConfigItem globalParamConfigItem = new GlobalParamConfigItem();
			if (num2 == 0)
			{
				globalParamConfigItem.IsSelected = true;
			}
			globalParamConfigItem.Parent = globalParamConfigItem;
			globalParamConfigItem.Owner = VmSolution.Instance;
			globalParamConfigItem.GlobalData = GlobalData;
			globalParamConfigItem.Name = list[num2];
			globalParamConfigItem.ID = list2[num2];
			GlobalParamItems.Add(globalParamConfigItem);
		}
		OnPropertyChanged("IsEmpty");
		OnPropertyChanged("GlobalOptions");
	}

	public void SaveData()
	{
		string text = "";
		string text2 = "";
		for (int i = 0; i < GlobalParamItems.Count; i++)
		{
			if (i > 0)
			{
				text += ";";
				text2 += ";";
			}
			text += GlobalParamItems[i].Name;
			text2 += GlobalParamItems[i].ID;
		}
		VmSolution.Instance.UserData["GlobalParamNameKey"] = (string.IsNullOrEmpty(text) ? null : StringUtil.UTF8GetBytesPadZero(text));
		VmSolution.Instance.UserData["GlobalParamIdKey"] = (string.IsNullOrEmpty(text2) ? null : StringUtil.UTF8GetBytesPadZero(text2));
	}

	private void EditCommandExecute()
	{
		foreach (GlobalParamConfigItem globalParamItem in GlobalParamItems)
		{
			globalParamItem.Collection.Clear();
			if (globalParamItem.GroupCollection != null)
			{
				foreach (DynamicNameValueItemOfGroupCategory item3 in globalParamItem.GroupCollection)
				{
					globalParamItem.Collection.Add(ParamConfigDataBase.GetRunParamConfigDecorateForGroup(item3));
				}
			}
			foreach (DynamicNameValueItemOfGroup item in globalParamItem.ParamCollection)
			{
				RunParamConfigDecorate item2 = globalParamItem.Collection.LastOrDefault((RunParamConfigDecorate p) => p.GroupName == item.Group);
				int index = globalParamItem.Collection.IndexOf(item2);
				RunParamConfigDecorate runParamConfigDecorate = new RunParamConfigDecorate
				{
					DecorateSource = item,
					Owner = VmSolution.Instance
				};
				runParamConfigDecorate.DecorateSource.SetParentNode(globalParamItem);
				globalParamItem.Collection.Insert(index, runParamConfigDecorate);
			}
		}
		GlobalData.GlobalParamItems = GlobalParamItems;
		GlobalData.SelectedGlobalItem = GlobalParamItems.FirstOrDefault((GlobalParamConfigItem p) => p.IsSelected);
		GlobalParamConfigWindow globalParamConfigWindow = new GlobalParamConfigWindow();
		globalParamConfigWindow.DataContext = GlobalData;
		GlobalData.RelateControl = globalParamConfigWindow;
		GlobalData.AsyncLoadParams();
		globalParamConfigWindow.ShowDialog();
		GlobalData.UpdateParams();
		OnPropertyChanged("IsEmpty");
		OnPropertyChanged("GlobalOptions");
		if (CVmObjectBase.GetIsSDKMode())
		{
			SaveData();
		}
	}

	private void SelectCommandExecute(string param)
	{
		if (!string.IsNullOrEmpty(param) && GlobalParamItems != null)
		{
			GlobalParamConfigItem globalParamConfigItem = GlobalParamItems.FirstOrDefault((GlobalParamConfigItem p) => p.Name == param);
			if (globalParamConfigItem != null)
			{
				SelectedGlobalItem = globalParamConfigItem;
			}
		}
	}

	private void MultiRun()
	{
		try
		{
			if (CheckRun())
			{
				((IVMRun)VmSolution.Instance).ContinuousRunEnable = true;
				IsMultiRun = true;
			}
		}
		catch (VmException ex)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}

	private void SigRun()
	{
		try
		{
			if (CheckRun())
			{
				VmSolution.Instance.Run();
			}
		}
		catch (VmException ex)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_GlobalScript") + ":" + ErrorCodeInfo.GetErrorInfo(ex.errorCode);
			HikMessageBox.Show(strMsg, MessageIcons.Error);
		}
	}

	private void StopRun()
	{
		((IVMRun)VmSolution.Instance).ContinuousRunEnable = false;
		IsMultiRun = false;
	}

	private bool CheckRun()
	{
		IEnumerable<VmProcedureDecorator> enumerable = from p in VmSolution.Instance.Modules.OfType<VmProcedure>()
			select new VmProcedureDecorator
			{
				DecorateSource = p
			};
		VmProcedureDecorator vmProcedureDecorator = enumerable.FirstOrDefault((VmProcedureDecorator p) => p.WorkState != StatusBarSymbol.Ready && p.IsEnabled);
		if (vmProcedureDecorator != null)
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

	private void UpdateDecorate(IDecorate decorate)
	{
		ImageDecorate = decorate;
	}

	private void ClosingCommandExecute()
	{
		SaveData();
		GlobalData.UpdateDecorateEvent -= UpdateDecorate;
		ImageDecorate = null;
	}
}
