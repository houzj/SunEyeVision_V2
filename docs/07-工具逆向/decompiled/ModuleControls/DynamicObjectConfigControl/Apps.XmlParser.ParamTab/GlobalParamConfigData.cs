using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.Common;
using DynamicObjectConfigControl.UI.Data;
using DynamicObjectConfigControl.XmlData;
using Hik.Xml;
using HikExternalCall.Common;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GlobalParamConfigData : NotifyPropertyChangedBase, IGlobalData, IParamList
{
	private AlgorithmTabRoot _AlgorithmTabRoot = new AlgorithmTabRoot(VmSolution.Instance.Params);

	private Dictionary<uint, ObservableCollection<DynamicNameValueItemOfGroup>> ProcedureParams = new Dictionary<uint, ObservableCollection<DynamicNameValueItemOfGroup>>();

	private List<string> _ParamList;

	private IList _EnumEntrysA;

	private GroupRunParamSelectableTypeItem _ComboBoxSelectedItem = ParamTypeList.DefaultItem;

	private string _SearchWord = "";

	private ObservableCollection<GlobalParamConfigItem> _GlobalParamItems;

	private GlobalParamConfigItem _SelectedGlobalItem;

	private ObservableCollection<DynamicNameValueItemOfGroup> _AllDynamicParams;

	private bool _isUnfold;

	internal IRelateControl RelateControl;

	private DelegateCommand<string> _TextChangedCommand = null;

	private DelegateCommand<object> _CheckCommand = null;

	private DelegateCommand _AddGlobalParamCommand = null;

	ObservableCollection<DynamicNameValueItemOfGroup> IGlobalData.AllParamCollection => AllDynamicParams;

	ObservableCollection<GlobalParamConfigItem> IGlobalData.GlobalParamItems => GlobalParamItems;

	IList IGlobalData.EnumEntrys => EnumEntrysA;

	AlgorithmTabRoot IGlobalData.SolutionTabRoot => _AlgorithmTabRoot;

	List<string> IParamList.ParamList
	{
		get
		{
			if (_ParamList == null)
			{
				_ParamList = new List<string>();
				foreach (GlobalParamConfigItem globalParamItem in GlobalParamItems)
				{
					foreach (RunParamConfigDecorate item in globalParamItem.Collection)
					{
						if (!item.IsForGroup)
						{
							_ParamList.Add(item.UniquePath);
						}
					}
				}
			}
			return _ParamList;
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			return _EnumEntrysA;
		}
		set
		{
			_EnumEntrysA = value;
			((NotifyPropertyChangedBase)this).OnPropertyChanged("EnumEntrysA");
		}
	}

	public IList SelectedType { get; } = ParamTypeList.SelectableList;

	public GroupRunParamSelectableTypeItem ComboBoxSelectedItem
	{
		get
		{
			return _ComboBoxSelectedItem;
		}
		set
		{
			if (_ComboBoxSelectedItem != value)
			{
				_ComboBoxSelectedItem = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("ComboBoxSelectedItem");
				AsyncLoadParams();
			}
		}
	}

	public string SearchWord
	{
		get
		{
			return _SearchWord;
		}
		set
		{
			if (_SearchWord != value)
			{
				_SearchWord = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("SearchWord");
				AsyncLoadParams();
			}
		}
	}

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
				((NotifyPropertyChangedBase)this).OnPropertyChanged("GlobalParamItems");
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
				((NotifyPropertyChangedBase)this).OnPropertyChanged("SelectedGlobalItem");
			}
		}
	}

	public ObservableCollection<DynamicNameValueItemOfGroup> AllDynamicParams
	{
		get
		{
			if (_AllDynamicParams == null)
			{
				_AllDynamicParams = new ObservableCollection<DynamicNameValueItemOfGroup>();
				List<string> allDynamicParam = ((IVmModuleSet)VmSolution.Instance).GetAllDynamicParam();
				Dictionary<string, NameValueItem> allDynamicParams = GetAllDynamicParams(allDynamicParam);
				if (allDynamicParams != null)
				{
					foreach (KeyValuePair<string, NameValueItem> item in allDynamicParams)
					{
						DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup = new DynamicNameValueItemOfGroup
						{
							Name = item.Key,
							CloneSource = allDynamicParams[item.Key]
						};
						dynamicNameValueItemOfGroup.SetParentNode(_AlgorithmTabRoot);
						dynamicNameValueItemOfGroup.CloneSource.Name = item.Key;
						dynamicNameValueItemOfGroup.CloneSource.DisplayName = item.Key;
						dynamicNameValueItemOfGroup.CloneSource.SetParentNode(dynamicNameValueItemOfGroup);
						_AllDynamicParams.Add(dynamicNameValueItemOfGroup);
					}
				}
			}
			return _AllDynamicParams;
		}
	}

	public bool IsUnfold
	{
		get
		{
			return _isUnfold;
		}
		set
		{
			if (value == _isUnfold)
			{
				return;
			}
			_isUnfold = value;
			EnumEntrysA.OfType<ProcedureHolder>().ToList().ForEach((ProcedureHolder p) =>
			{
				p.IsExpanded = value;
				p.EnumEntrysA.OfType<GroupHolder>().ToList().ForEach((GroupHolder q) =>
				{
					q.IsExpanded = value;
				});
			});
			((NotifyPropertyChangedBase)this).OnPropertyChanged("IsUnfold");
		}
	}

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedCommandExecute)));

	public ICommand CheckCommand => (ICommand)(_CheckCommand ?? (_CheckCommand = new DelegateCommand<object>((Action<object>)CheckCommandExecute)));

	public ICommand AddGlobalParamCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _AddGlobalParamCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)AddGlobalParamCommandExecute);
				DelegateCommand val3 = val2;
				_AddGlobalParamCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public Func<object, bool> CheckClosing => CheckClosingMethod;

	public event UpdateDecorateDelegate UpdateDecorateEvent;

	public ObservableCollection<DynamicNameValueItemOfGroup> GetProcedureParams(VmProcedure procedure)
	{
		ObservableCollection<DynamicNameValueItemOfGroup> observableCollection = new ObservableCollection<DynamicNameValueItemOfGroup>();
		if (procedure != null)
		{
			if (ProcedureParams.ContainsKey(procedure.ID))
			{
				return ProcedureParams[procedure.ID];
			}
			List<string> allDynamicParam = ((IVmModuleSet)procedure).GetAllDynamicParam();
			Dictionary<string, NameValueItem> allDynamicParamConfig = GetAllDynamicParamConfig(procedure, allDynamicParam);
			if (allDynamicParamConfig != null)
			{
				BaseNode parentNode = procedure.Params.GetParamRoot().FindBaseNodeByName("Tab_Run Params");
				foreach (KeyValuePair<string, NameValueItem> item in allDynamicParamConfig)
				{
					if (allDynamicParam.Contains(item.Key))
					{
						DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup = new DynamicNameValueItemOfGroup
						{
							Name = item.Key,
							CloneSource = allDynamicParamConfig[item.Key]
						};
						dynamicNameValueItemOfGroup.SetParentNode(parentNode);
						dynamicNameValueItemOfGroup.CloneSource.Name = item.Key;
						dynamicNameValueItemOfGroup.CloneSource.DisplayName = item.Key;
						dynamicNameValueItemOfGroup.CloneSource.SetParentNode(dynamicNameValueItemOfGroup);
						observableCollection.Add(dynamicNameValueItemOfGroup);
					}
				}
			}
			ProcedureParams.Add(procedure.ID, observableCollection);
		}
		return observableCollection;
	}

	private Dictionary<string, NameValueItem> GetAllDynamicParamConfig(VmModuleSet vmModuleSet, List<string> tempDic)
	{
		try
		{
			Dictionary<string, NameValueItem> dictionary = new Dictionary<string, NameValueItem>();
			string key = VmModuleSet.IndexListName ?? "";
			List<int> list = ExternalCallHelper.BytesToStructs<int>(vmModuleSet.UserData[key]);
			if (list == null)
			{
				LogHelper.Error("Dynamic param index list is null.");
				return null;
			}
			string[] array = new string[tempDic.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[list[i]] = tempDic[i];
			}
			string[] array2 = array;
			foreach (string text in array2)
			{
				NameValueItem nameValueItem = GroupRunParam.LoadNameValueItemByName(vmModuleSet, text);
				if (nameValueItem != null)
				{
					dictionary.Add(text, nameValueItem);
				}
			}
			return dictionary;
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetAllDynamicParamConfig Error:" + ex.Message);
			return null;
		}
	}

	public void OnUpdateDecorateEvent(IDecorate decorate)
	{
		if (UpdateDecorateEvent != null)
		{
			UpdateDecorateEvent(decorate);
		}
	}

	public void AsyncLoadParams()
	{
		Task.Run(() =>
		{
			RelateControl?.ShowProgressDialog("正在加载数据");
			_ParamList = null;
			List<Holder> list = new List<Holder>();
			IEnumerable<IVmModule> enumerable = from p in ((IChildren)VmSolution.Instance).Children.OfType<IVmModule>()
				where p.ID != 11000 && (p.IsGlobalModu || p is IVmProcedure)
				select p;
			int num = enumerable.Count();
			int num2 = 0;
			int num3 = 0;
			foreach (IVmModule item in enumerable)
			{
				ProcedureHolder procedureHolder = new ProcedureHolder
				{
					DisplayNameA = "%" + item.Name + "%",
					filterTypes = ComboBoxSelectedItem.TypeList,
					searchWord = SearchWord,
					HolderTarget = item,
					GlobalData = this
				};
				try
				{
					if (procedureHolder.EnumEntrysA.Count > 0)
					{
						list.Add(procedureHolder);
					}
				}
				catch (Exception ex)
				{
					LogHelper.Error("Module [" + item.Name + "] load param error: " + ex.Message + Environment.NewLine + ex.StackTrace);
				}
				num2++;
				num3 = 10 + (int)((float)num2 / (float)num * 90f);
				RelateControl?.SetProgressValue(num3);
			}
			EnumEntrysA = list;
			RelateControl?.CloseProgressDialog();
		});
	}

	private void TextChangedCommandExecute(string searchWord)
	{
		SearchWord = searchWord;
	}

	protected void CheckCommandExecute(object source)
	{
		if (!(source is Holder { CheckEnabled: var checkEnabled } holder))
		{
			return;
		}
		if (checkEnabled.Value)
		{
			if (GlobalParamItems.Count == 0)
			{
				AddGlobalParamCommandExecute();
			}
			if (SelectedGlobalItem == null)
			{
				holder.CheckEnabled = false;
			}
			else if (holder is ProcedureHolder procedureHolder)
			{
				AddProcedure(procedureHolder);
			}
			else if (holder is GroupHolder groupHolder)
			{
				AddGroup(groupHolder);
			}
			else
			{
				AddParam(holder);
			}
		}
		else if (holder is ProcedureHolder procedureHolder2)
		{
			DeleteProcedure(procedureHolder2);
		}
		else if (holder is GroupHolder groupHolder2)
		{
			DeleteGroup(groupHolder2);
		}
		else
		{
			DeleteParam(holder);
		}
	}

	private bool AddParam(Holder holder)
	{
		bool flag = false;
		if (holder != null && holder.HolderTarget is NameValueItem source)
		{
			flag = SelectedGlobalItem.AddParam(source);
			if (!flag)
			{
				holder.CheckEnabled = false;
			}
		}
		return flag;
	}

	private bool AddGroup(GroupHolder groupHolder)
	{
		if (groupHolder == null)
		{
			return false;
		}
		bool flag = true;
		foreach (Holder item in groupHolder.EnumEntrysA)
		{
			if (!flag)
			{
				item.CheckEnabled = false;
			}
			else
			{
				flag = AddParam(item);
			}
		}
		return flag;
	}

	private bool AddProcedure(ProcedureHolder procedureHolder)
	{
		if (procedureHolder == null)
		{
			return false;
		}
		bool flag = true;
		foreach (Holder item in procedureHolder.EnumEntrysA)
		{
			if (!flag)
			{
				item.CheckEnabled = false;
			}
			else
			{
				flag = ((!(item is GroupHolder groupHolder)) ? AddParam(item) : AddGroup(groupHolder));
			}
		}
		return flag;
	}

	private bool? DeleteParam(Holder holder)
	{
		bool? result = null;
		if (holder != null)
		{
			NameValueItem item = holder.HolderTarget as NameValueItem;
			if (item != null)
			{
				foreach (GlobalParamConfigItem globalParamItem in GlobalParamItems)
				{
					RunParamConfigDecorate runParamConfigDecorate = globalParamItem.Collection.FirstOrDefault((RunParamConfigDecorate p) => !p.IsForGroup && ParamConfigDataBase.CheckParam(item, p.UniquePath));
					if (runParamConfigDecorate != null)
					{
						result = globalParamItem.DeleteParam(runParamConfigDecorate);
						if (result.Value && !(item.ParamsSource is VmModuleSet) && item.ParamsSource is VmModule vmModule)
						{
							vmModule.UserData[VmModuleSet.HeadPathPre + item.DisplayName] = null;
						}
						break;
					}
				}
			}
			if (result.HasValue && !result.Value)
			{
				holder.CheckEnabled = true;
			}
		}
		return result;
	}

	private void DeleteGroup(GroupHolder groupHolder)
	{
		if (groupHolder == null)
		{
			return;
		}
		foreach (Holder item in groupHolder.EnumEntrysA)
		{
			DeleteParam(item);
		}
	}

	private void DeleteProcedure(ProcedureHolder procedureHolder)
	{
		if (procedureHolder == null)
		{
			return;
		}
		foreach (Holder item in procedureHolder.EnumEntrysA)
		{
			if (item is GroupHolder groupHolder)
			{
				DeleteGroup(groupHolder);
			}
			else
			{
				DeleteParam(item);
			}
		}
	}

	private void AddGlobalParamCommandExecute()
	{
		GlobalParamConfigItem globalParamConfigItem = new GlobalParamConfigItem();
		globalParamConfigItem.Parent = globalParamConfigItem;
		globalParamConfigItem.Owner = VmSolution.Instance;
		globalParamConfigItem.GlobalData = this;
		globalParamConfigItem.Name = GetCustomName();
		globalParamConfigItem.ID = GetCustomId();
		globalParamConfigItem.IsSelected = true;
		GlobalParamItems.Add(globalParamConfigItem);
	}

	private string GetCustomName()
	{
		int num = 1;
		string name = string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_GlobalParam"), num);
		while (GlobalParamItems.Any((GlobalParamConfigItem p) => p.Name == name))
		{
			num++;
			name = string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_GlobalParam"), num);
		}
		return name;
	}

	private int GetCustomId()
	{
		int id;
		for (id = 0; GlobalParamItems.Any((GlobalParamConfigItem p) => p.ID == id); id++)
		{
		}
		return id;
	}

	private bool CheckClosingMethod(object data)
	{
		if (data is ModulesPickData modulesPickData)
		{
			ObservableCollection<object> observableCollection = ((modulesPickData.EnumEntrysA.Count == 0) ? null : (modulesPickData.EnumEntrysA[0] as Holder).EnumEntrysA);
			if (observableCollection != null)
			{
				bool flag = observableCollection.OfType<Holder>().Count((Holder p) => p.CheckEnabled.Value) > 128;
				if (flag)
				{
					HikMessageBox.Show(string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_ExceedConfigNumLimit"), 128), MessageIcons.Warning);
				}
				return !flag;
			}
		}
		return true;
	}

	public void UpdateParams()
	{
		_AllDynamicParams = null;
		foreach (GlobalParamConfigItem globalParamItem in GlobalParamItems)
		{
			globalParamItem.UpdateParams();
			IEnumerable<DynamicNameValueItemOfGroupCategory> enumerable = globalParamItem.Collection.Select((RunParamConfigDecorate p) => p.GroupName).Distinct();
			foreach (DynamicNameValueItemOfGroupCategory group in enumerable)
			{
				DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = globalParamItem.GroupCollection?.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.ID == group.ID);
				if (dynamicNameValueItemOfGroupCategory != null)
				{
					dynamicNameValueItemOfGroupCategory.IsExpanded = group.IsExpanded;
				}
			}
		}
	}

	private Dictionary<string, NameValueItem> GetAllDynamicParams(List<string> tempDic)
	{
		try
		{
			Dictionary<string, NameValueItem> dictionary = new Dictionary<string, NameValueItem>();
			if (tempDic != null)
			{
				foreach (string item in tempDic)
				{
					NameValueItem nameValueItem = GroupRunParam.LoadNameValueItemByName(VmSolution.Instance, item);
					if (nameValueItem != null)
					{
						dictionary.Add(item, nameValueItem);
					}
				}
			}
			return dictionary;
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetAllDynamicParams Error:" + ex.Message);
			return null;
		}
	}
}
