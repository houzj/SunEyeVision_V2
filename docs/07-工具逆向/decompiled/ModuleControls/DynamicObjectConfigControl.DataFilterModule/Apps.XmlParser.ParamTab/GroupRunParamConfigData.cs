using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.UIHelper.Extern;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.UI.Data;
using DynamicObjectConfigControl.XmlData;
using HikExternalCall.Common;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GroupRunParamConfigData : NotifyPropertyChangedBase
{
	private static int MaxRunParamsNum;

	private IList _EnumEntrysA;

	private static GroupRunParamSelectableTypeItem DefaultItem;

	internal static List<GroupRunParamSelectableTypeItem> SelectableList;

	private GroupRunParamSelectableTypeItem _ComboBoxSelectedItem = DefaultItem;

	private string _SearchWord = "";

	internal VmModuleSet Owner;

	internal GroupRunParam Parent;

	private DelegateCommand<object> _CheckCommand = null;

	private DelegateCommand<string> _TextChangedCommand = null;

	private ICommand _DelCommand = null;

	private object selectedItem;

	private ICommand _UpCommand = null;

	private ICommand _DownCommand = null;

	private ICommand _DelAllCommand = null;

	private ICommand _AddGroupCommand = null;

	private ICommand _DelGroupCommand = null;

	private ICommand _UpGroupCommand = null;

	private ICommand _DownGroupCommand = null;

	public IList EnumEntrysA
	{
		get
		{
			if (_EnumEntrysA == null)
			{
				RunParamPickData runParamPickData = new RunParamPickData();
				runParamPickData.Node = Owner;
				runParamPickData.ConfigData = this;
				runParamPickData.FilterTypes = ComboBoxSelectedItem.TypeList;
				runParamPickData.SearchWord = SearchWord;
				runParamPickData.Init();
				_EnumEntrysA = runParamPickData.EnumEntrysA;
			}
			return _EnumEntrysA;
		}
	}

	public IList<RunParamConfigDecorate> Collection { get; set; } = new ObservableCollection<RunParamConfigDecorate>();

	public IList SelectedType { get; } = SelectableList;

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
				_EnumEntrysA = null;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("EnumEntrysA");
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
				_EnumEntrysA = null;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public ICommand CheckCommand => (ICommand)(_CheckCommand ?? (_CheckCommand = new DelegateCommand<object>((Action<object>)CheckCommandExecute)));

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedCommandExecute)));

	public ICommand DelCommand => _DelCommand ?? (_DelCommand = (ICommand)new DelegateCommand<RunParamConfigDecorate>((Action<RunParamConfigDecorate>)DelCommandExecute));

	public object SelectedItem
	{
		get
		{
			return selectedItem;
		}
		set
		{
			if (selectedItem != value)
			{
				selectedItem = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("SelectedItem");
			}
		}
	}

	public ICommand UpCommand => _UpCommand ?? (_UpCommand = (ICommand)new DelegateCommand<object>((Action<object>)UpCommandExecute, (Func<object, bool>)CanUpCommandExecute, false));

	public ICommand DownCommand => _DownCommand ?? (_DownCommand = (ICommand)new DelegateCommand<object>((Action<object>)DownCommandExecute, (Func<object, bool>)CanDownCommandExecute, false));

	public ICommand DelAllCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _DelAllCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)DelAllCommandExecute);
				ICommand command2 = (ICommand)val;
				_DelAllCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand AddGroupCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _AddGroupCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)AddGroupCommandExecute);
				ICommand command2 = (ICommand)val;
				_AddGroupCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand DelGroupCommand => _DelGroupCommand ?? (_DelGroupCommand = (ICommand)new DelegateCommand<object>((Action<object>)DelGroupCommandExecute));

	public ICommand UpGroupCommand => _UpGroupCommand ?? (_UpGroupCommand = (ICommand)new DelegateCommand<object>((Action<object>)UpGroupCommandExecute, (Func<object, bool>)CanUpGroupCommandExecute, false));

	public ICommand DownGroupCommand => _DownGroupCommand ?? (_DownGroupCommand = (ICommand)new DelegateCommand<object>((Action<object>)DownGroupCommandExecute, (Func<object, bool>)CanDownGroupCommandExecute, false));

	public Func<object, bool> CheckClosing => CheckClosingMethod;

	static GroupRunParamConfigData()
	{
		MaxRunParamsNum = 32;
		DefaultItem = new GroupRunParamSelectableTypeItem
		{
			Name = "All"
		};
		SelectableList = new List<GroupRunParamSelectableTypeItem>
		{
			new GroupRunParamSelectableTypeItem
			{
				Name = "numerical_value",
				TypeList = new List<string> { "Integer", "Float", "IntegerBettween", "FloatBettween", "IntegerBettween_CircleFind", "Integer_CircleFind" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Enum",
				TypeList = new List<string> { "Enumeration" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "on_off",
				TypeList = new List<string> { "Boolean", "BooleanTips" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Single_Select",
				TypeList = new List<string> { "RadioSelecter", "RadioSelecterEx2" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Lang_ROISet",
				TypeList = new List<string> { "ROISelecter", "ROISelecter_Extern" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "File_Path",
				TypeList = new List<string> { "OpenFileDialog", "OpenFileForCNNDialog", "OpenFileForCNNUSDialog", "OpenFileForCalibDialog", "OpenFileForCameraDialog", "OpenFileForDataShellDialog", "OpenFolderDialog", "OpenFileDialog4ReadFile" }
			},
			new GroupRunParamSelectableTypeItem
			{
				Name = "Train Pattern Settings",
				TypeList = new List<string> { "FeaturepatMatchConfigPanalData", "FastFeaturepatMatchConfigPanalData", "PyramidFeaturepatMatchConfigPanalData", "GrayMatchVAConfigPanalData", "GrayMatchConfigPanalData" }
			}
		};
		DefaultItem.TypeList = SelectableList.SelectMany((GroupRunParamSelectableTypeItem p) => p.TypeList).ToList();
		SelectableList.Insert(0, DefaultItem);
		try
		{
			if (int.TryParse(ConfigurationHelper.GetConfig("MaxRunParamsNum"), out var result))
			{
				MaxRunParamsNum = result;
			}
		}
		catch
		{
		}
	}

	protected void CheckCommandExecute(object source)
	{
		if (!(source is Holder { CheckEnabled: var checkEnabled } holder))
		{
			return;
		}
		if (checkEnabled.Value)
		{
			if (holder.HolderTarget is VmModule)
			{
				foreach (Holder item3 in holder.EnumEntrysA)
				{
					if (item3.HolderTarget is NameValueItem source2 && !AddParam(source2))
					{
						break;
					}
				}
				return;
			}
			if (holder.HolderTarget is NameValueItem source3)
			{
				AddParam(source3);
			}
			return;
		}
		if (holder.HolderTarget is VmModule)
		{
			foreach (Holder item4 in holder.EnumEntrysA)
			{
				NameValueItem item = item4.HolderTarget as NameValueItem;
				if (item != null)
				{
					RunParamConfigDecorate runParamConfigDecorate = Collection.FirstOrDefault((RunParamConfigDecorate p) => !p.IsForGroup && p.RelationModulesStr == (item.ParamsSource as VmModule).Name && p.Name.Contains(AppLocalizationService.GetLang(item.DisplayNameA)));
					if (runParamConfigDecorate != null && !DeleteParam(runParamConfigDecorate))
					{
						break;
					}
				}
			}
			return;
		}
		NameValueItem item2 = holder.HolderTarget as NameValueItem;
		if (item2 != null)
		{
			RunParamConfigDecorate runParamConfigDecorate2 = Collection.FirstOrDefault((RunParamConfigDecorate p) => !p.IsForGroup && p.RelationModulesStr == (item2.ParamsSource as VmModule).Name && p.Name.Contains(AppLocalizationService.GetLang(item2.DisplayNameA)));
			if (runParamConfigDecorate2 != null)
			{
				DeleteParam(runParamConfigDecorate2);
			}
		}
	}

	private void TextChangedCommandExecute(string searchWord)
	{
		SearchWord = searchWord;
	}

	private bool AddParam(NameValueItem source)
	{
		if (Enumerable.Count(Collection, (RunParamConfigDecorate p) => !p.IsForGroup) >= MaxRunParamsNum)
		{
			HikMessageBox.Show(string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_ExceedConfigNumLimit"), MaxRunParamsNum), MessageIcons.Warning);
			return false;
		}
		if (Collection.Count == 0)
		{
			AddGroupCommandExecute();
		}
		DynamicNameValueItemOfGroupCategory group = Parent.GroupCollection.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.IsSelected);
		if (group == null)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_Warning_SelectGroup"), MessageIcons.Warning);
			return false;
		}
		NameValueItem nameValueItem = null;
		if (source is DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup)
		{
			nameValueItem = GroupRunParam.LoadNameValueItemByName(dynamicNameValueItemOfGroup.ParamsSource as VmModuleSet, source.Name);
		}
		else
		{
			nameValueItem = ((!(source is INeedGroupRunParamSubstitute needGroupRunParamSubstitute)) ? source.Clone<NameValueItem>() : (needGroupRunParamSubstitute.GetGroupRunParamSubstitute(null) as NameValueItem));
			IniteClone(nameValueItem);
		}
		nameValueItem.SetParentNode(Parent);
		nameValueItem.SubscribeVisible = false;
		string text = (nameValueItem.Name = (nameValueItem.DisplayName = AppLocalizationService.UnDoTranslate(GetCustomName(AppLocalizationService.GetLang(source.DisplayNameA)))));
		nameValueItem.CustomVisible = true;
		RunParamConfigDecorate runParamConfigDecorate = new RunParamConfigDecorate
		{
			Owner = Owner,
			DecorateSource = new DynamicNameValueItemOfGroup
			{
				CloneSource = nameValueItem,
				Name = text
			}
		};
		runParamConfigDecorate.DecorateSource.SetParentNode(Parent);
		runParamConfigDecorate.GroupName = group;
		runParamConfigDecorate.SetGroupID(group.ID);
		group.IsExpanded = true;
		RunParamConfigDecorate item = Collection.Last((RunParamConfigDecorate p) => p.GroupName == group);
		int index = Collection.IndexOf(item);
		Collection.Insert(index, runParamConfigDecorate);
		DynamicNameValueItemOfGroup item2 = group.Collection.LastOrDefault();
		group.collection.Add(runParamConfigDecorate.DecorateSource);
		ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParam(Owner.ID, text);
		byte[] value = StringUtil.UTF8GetBytesPadZero(nameValueItem.ToXmlString(IsShort: true));
		List<int> indexList = GetIndexList(Owner);
		int num = Parent.Collection.IndexOf(item2);
		if (num < 0)
		{
			item2 = Parent.Collection.LastOrDefault((DynamicNameValueItemOfGroup p) => Parent.GroupCollection.IndexOf(p.Group) < Parent.GroupCollection.IndexOf(group));
			num = ((item2 != null) ? Parent.Collection.IndexOf(item2) : (-1));
		}
		for (int num2 = 0; num2 < indexList.Count; num2++)
		{
			if (indexList[num2] > num)
			{
				indexList[num2]++;
			}
		}
		indexList.Add(num + 1);
		Parent.Collection.Insert(num + 1, runParamConfigDecorate.DecorateSource);
		SetIndexList(Owner, indexList);
		string key = $"{VmModuleSet.HeadValuePre}{text}";
		Owner.UserData[key] = value;
		string key2 = $"{VmModuleSet.HeadPathPre}{text}";
		if (source.ParamsSource is VmModuleSet vmModuleSet)
		{
			Owner.UserData[key2] = vmModuleSet.UserData[$"{VmModuleSet.HeadPathPre}{source.Name}"];
		}
		else
		{
			Owner.UserData[key2] = StringUtil.UTF8GetBytesPadZero($"{(source.ParamsSource as VmModule).StrModuleName}.{source.Name}.{source.DisplayName}");
		}
		ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParamRelation(Owner.ID, text, (source.ParamsSource as IVmModule).ID, source.Name);
		return true;
	}

	public static List<int> GetIndexList(VmModule vmModule)
	{
		string key = $"{VmModuleSet.IndexListName}";
		return ExternalCallHelper.BytesToStructs<int>(vmModule.UserData[key]) ?? new List<int>();
	}

	public static void SetIndexList(VmModule vmModule, List<int> ls)
	{
		string key = $"{VmModuleSet.IndexListName}";
		vmModule.UserData[key] = ExternalCallHelper.StructsToBytes(ls);
	}

	private void IniteClone(NameValueItem newObj)
	{
		if (newObj.Triggers != null)
		{
			newObj.Triggers.Clear();
		}
		if (newObj.Initers != null)
		{
			newObj.Initers.Clear();
		}
		if (newObj.ExternIniters != null)
		{
			newObj.ExternIniters.Clear();
		}
		if (newObj.DeIniters != null)
		{
			newObj.DeIniters.Clear();
		}
	}

	private string GetCustomName(string displayNameA)
	{
		string preName = displayNameA;
		int num = 1;
		while (Collection.Any((RunParamConfigDecorate p) => !p.IsForGroup && p.Name == AppLocalizationService.UnDoTranslate(preName)))
		{
			preName = $"{displayNameA}{num}";
			num++;
		}
		return preName;
	}

	private void DelCommandExecute(RunParamConfigDecorate runParamConfigDecorate)
	{
		string relationModulesStr = runParamConfigDecorate.RelationModulesStr;
		if (!DeleteParam(runParamConfigDecorate))
		{
			return;
		}
		bool flag = false;
		foreach (Holder item in EnumEntrysA)
		{
			foreach (Holder item2 in item.EnumEntrysA)
			{
				if (item2.HolderTarget is NameValueItem nameValueItem && relationModulesStr == (nameValueItem.ParamsSource as VmModule).Name && runParamConfigDecorate.Name.Contains(AppLocalizationService.GetLang(nameValueItem.DisplayNameA)))
				{
					item2.CheckEnabled = false;
					flag = true;
					break;
				}
			}
			if (flag)
			{
				break;
			}
		}
	}

	private bool DeleteParam(RunParamConfigDecorate runParamConfigDecorate)
	{
		if (!runParamConfigDecorate.IsForGroup)
		{
			try
			{
				ServerSDKManager.serverSDKManager.mModuleManager.DeleteDynamicParam(Owner.ID, runParamConfigDecorate.Name);
			}
			catch (VmException vmException)
			{
				ShowMessage.ShowVmExceptionMessage(vmException);
				return false;
			}
			DelUserDataByName(Owner, runParamConfigDecorate.Name, (from p in Collection
				where !p.IsForGroup
				select p.Name).ToList());
		}
		Collection.Remove(runParamConfigDecorate);
		Parent.Collection.Remove(runParamConfigDecorate.DecorateSource);
		runParamConfigDecorate.GroupName.Collection.Remove(runParamConfigDecorate.DecorateSource);
		return true;
	}

	internal static void DelUserDataByName(VmModule vmModule, string name, IList<string> names)
	{
		int num = names.IndexOf(name);
		List<int> indexList = GetIndexList(vmModule);
		indexList.Remove(num);
		for (int i = 0; i < indexList.Count; i++)
		{
			if (indexList[i] > num)
			{
				indexList[i]--;
			}
		}
		SetIndexList(vmModule, indexList);
		string key = $"{VmModuleSet.HeadValuePre}{name}";
		vmModule.UserData[key] = null;
		string key2 = $"{VmModuleSet.HeadPathPre}{name}";
		vmModule.UserData[key2] = null;
		string key3 = $"{VmModuleSet.GroupIDPre}{name}";
		vmModule.UserData[key3] = null;
	}

	private void UpDownCommandExecute(object data, bool isUp)
	{
		RunParamConfigDecorate runParamConfigDecorate = data as RunParamConfigDecorate;
		List<RunParamConfigDecorate> list = Collection.Where((RunParamConfigDecorate p) => !p.IsForGroup).ToList();
		int num = list.IndexOf(runParamConfigDecorate);
		if (isUp)
		{
			if (num < 1)
			{
				return;
			}
		}
		else if (num > list.Count - 1)
		{
			return;
		}
		int num2 = runParamConfigDecorate.GroupName.Collection.IndexOf(runParamConfigDecorate.DecorateSource) + ((!isUp) ? 1 : (-1));
		if (isUp)
		{
			if (num2 < 0)
			{
				return;
			}
		}
		else if (num2 > runParamConfigDecorate.GroupName.Collection.Count - 1)
		{
			return;
		}
		DynamicNameValueItemOfGroup preData = runParamConfigDecorate.GroupName.Collection[num2];
		RunParamConfigDecorate item = Collection.Single((RunParamConfigDecorate p) => p.DecorateSource == preData);
		int num3 = list.IndexOf(item);
		List<int> indexList = GetIndexList(runParamConfigDecorate.Owner);
		int num4 = 0;
		for (int num5 = 0; num5 < indexList.Count; num5++)
		{
			if (indexList[num5] == num || indexList[num5] == num3)
			{
				indexList[num5] = ((indexList[num5] == num) ? num3 : num);
				if (num4 == 1)
				{
					break;
				}
				num4++;
			}
		}
		SetIndexList(runParamConfigDecorate.Owner, indexList);
		num = Collection.IndexOf(runParamConfigDecorate);
		num3 = Collection.IndexOf(item);
		Collection.Remove(runParamConfigDecorate);
		Collection.Insert(num3, runParamConfigDecorate);
		Collection.Remove(item);
		Collection.Insert(num, item);
		runParamConfigDecorate.GroupName.Collection.Remove(runParamConfigDecorate.DecorateSource);
		runParamConfigDecorate.GroupName.Collection.Insert(num2, runParamConfigDecorate.DecorateSource);
		int index = Parent.Collection.IndexOf(runParamConfigDecorate.DecorateSource) + ((!isUp) ? 1 : (-1));
		Parent.Collection.Remove(runParamConfigDecorate.DecorateSource);
		Parent.Collection.Insert(index, runParamConfigDecorate.DecorateSource);
		SelectedItem = data;
	}

	private bool CanUpCommandExecute(object data)
	{
		RunParamConfigDecorate runParamConfigDecorate = data as RunParamConfigDecorate;
		return runParamConfigDecorate.GroupName.Collection.IndexOf(runParamConfigDecorate.DecorateSource) > 0;
	}

	private void UpCommandExecute(object data)
	{
		UpDownCommandExecute(data, isUp: true);
	}

	private bool CanDownCommandExecute(object data)
	{
		RunParamConfigDecorate runParamConfigDecorate = data as RunParamConfigDecorate;
		return runParamConfigDecorate.GroupName.Collection.IndexOf(runParamConfigDecorate.DecorateSource) < runParamConfigDecorate.GroupName.Collection.Count - 1;
	}

	private void DownCommandExecute(object data)
	{
		UpDownCommandExecute(data, isUp: false);
	}

	private void DelAllCommandExecute()
	{
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InfoTips"), AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.YesOrNo, MessageIcons.Question) != DialogResult.Yes)
		{
			return;
		}
		foreach (RunParamConfigDecorate item in Collection.ToList())
		{
			DelCommandExecute(item);
		}
		ChangeGroupInfosToSDK(null, null);
		Parent.GroupCollection.Clear();
	}

	private void AddGroupCommandExecute()
	{
		if (Parent.GroupCollection.Count >= MaxRunParamsNum)
		{
			HikMessageBox.Show(string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_ExceedConfigNumLimit"), MaxRunParamsNum), MessageIcons.Warning);
			return;
		}
		List<string> groupNames = GroupRunParam.GetGroupNames(Owner);
		List<int> groupIDs = GroupRunParam.GetGroupIDs(Owner);
		string newGroupName = GetNewGroupName(groupNames);
		int newGroupID = GetNewGroupID(groupIDs);
		DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = new DynamicNameValueItemOfGroupCategory
		{
			Parent = Parent,
			Name = newGroupName,
			ID = newGroupID
		};
		Parent.GroupCollection.Add(dynamicNameValueItemOfGroupCategory);
		dynamicNameValueItemOfGroupCategory.SelectCommandExecute();
		RunParamConfigDecorate runParamConfigDecorateForGroup = GetRunParamConfigDecorateForGroup(dynamicNameValueItemOfGroupCategory);
		Collection.Add(runParamConfigDecorateForGroup);
		groupNames.Add(newGroupName);
		groupIDs.Add(newGroupID);
		ChangeGroupInfosToSDK(groupNames, groupIDs);
	}

	public static RunParamConfigDecorate GetRunParamConfigDecorateForGroup(DynamicNameValueItemOfGroupCategory newGroup)
	{
		return new RunParamConfigDecorate
		{
			GroupName = newGroup,
			IsForGroup = true
		};
	}

	private void ChangeGroupInfosToSDK(List<string> groupNames, List<int> groupIDs)
	{
		DynamicNameValueItemOfGroupCategory.ChangeGroupNamesToSDK(Owner, groupNames);
		if (groupIDs == null)
		{
			Owner.UserData["ParamGroupIDsList"] = null;
			return;
		}
		string text = string.Join(";", groupIDs);
		Owner.UserData["ParamGroupIDsList"] = (string.IsNullOrEmpty(text) ? null : StringUtil.UTF8GetBytesPadZero(text));
	}

	private int GetNewGroupID(IEnumerable<int> id)
	{
		int i;
		for (i = 0; id.Contains(i); i++)
		{
		}
		return i;
	}

	private string GetNewGroupName(List<string> groupNames)
	{
		int num = 0;
		string lang = AppLocalizationService.GetLang("分组");
		string text = lang + num;
		while (groupNames.Contains(text))
		{
			num++;
			text = lang + num;
		}
		return text;
	}

	private void DelGroupCommandExecute(object data)
	{
		DynamicNameValueItemOfGroupCategory group = data as DynamicNameValueItemOfGroupCategory;
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InfoTips"), AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.YesOrNo, MessageIcons.Question) != DialogResult.Yes)
		{
			return;
		}
		List<string> groupNames = GroupRunParam.GetGroupNames(Owner);
		groupNames.Remove(group.Name);
		List<int> groupIDs = GroupRunParam.GetGroupIDs(Owner);
		groupIDs.Remove(group.ID);
		ChangeGroupInfosToSDK(groupNames, groupIDs);
		foreach (RunParamConfigDecorate item in Collection.Where((RunParamConfigDecorate p) => p.GroupName == group).ToList())
		{
			DelCommandExecute(item);
		}
		Parent.GroupCollection.Remove(group);
	}

	private void UpDownGroupCommandExecute2(object data, bool isUp)
	{
		DynamicNameValueItemOfGroupCategory runParamConfigDecorate = data as DynamicNameValueItemOfGroupCategory;
		int num = Parent.GroupCollection.IndexOf(runParamConfigDecorate) + 1;
		if (num > Parent.GroupCollection.Count - 1)
		{
			return;
		}
		DynamicNameValueItemOfGroupCategory preGroupData = Parent.GroupCollection[num];
		RunParamConfigDecorate item = Collection.Last((RunParamConfigDecorate p) => p.GroupName == preGroupData);
		int num2 = Collection.IndexOf(item);
		Parent.GroupCollection.Remove(runParamConfigDecorate);
		Parent.GroupCollection.Insert(num, runParamConfigDecorate);
		int index = num2;
		foreach (RunParamConfigDecorate item2 in Collection.Where((RunParamConfigDecorate p) => p.GroupName == runParamConfigDecorate).ToList())
		{
			Collection.Remove(item2);
			Collection.Insert(index, item2);
		}
		List<RunParamConfigDecorate> list = Collection.Where((RunParamConfigDecorate p) => p.GroupName == preGroupData).ToList();
		foreach (RunParamConfigDecorate item3 in list)
		{
			Collection.Remove(item3);
		}
		index = Collection.IndexOf(Collection.First((RunParamConfigDecorate p) => p.GroupName == runParamConfigDecorate));
		foreach (RunParamConfigDecorate item4 in list)
		{
			Collection.Insert(index, item4);
			index++;
		}
		int num3 = -1;
		int num4 = -1;
		int num5 = -1;
		int num6 = -1;
		DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup = preGroupData.collection.FirstOrDefault();
		if (dynamicNameValueItemOfGroup != null)
		{
			num3 = Parent.Collection.IndexOf(dynamicNameValueItemOfGroup);
			num4 = preGroupData.collection.Count;
			num5 = Parent.Collection.IndexOf(runParamConfigDecorate.collection.FirstOrDefault());
			num6 = runParamConfigDecorate.collection.Count;
		}
		if (runParamConfigDecorate.collection.Count > 0)
		{
			int num7 = Parent.Collection.IndexOf(Parent.Collection.First((DynamicNameValueItemOfGroup p) => p == runParamConfigDecorate.collection.First()));
			foreach (DynamicNameValueItemOfGroup item5 in preGroupData.Collection)
			{
				Parent.Collection.Remove(item5);
				Parent.Collection.Insert(num7, item5);
				num7++;
			}
		}
		List<string> groupNames = Parent.GroupCollection.Select((DynamicNameValueItemOfGroupCategory p) => p.Name).ToList();
		List<int> groupIDs = Parent.GroupCollection.Select((DynamicNameValueItemOfGroupCategory p) => p.ID).ToList();
		ChangeGroupInfosToSDK(groupNames, groupIDs);
		if (dynamicNameValueItemOfGroup == null)
		{
			return;
		}
		List<int> indexList = GetIndexList(Owner);
		List<int> list2 = new List<int>(num4);
		for (int num8 = num3; num8 < num3 + num4; num8++)
		{
			int num9 = indexList.IndexOf(num8);
			if (num9 < 0)
			{
				throw new Exception("不可能");
			}
			list2.Add(num9);
		}
		for (int num10 = num5; num10 < num5 + num6; num10++)
		{
			int num11 = indexList.IndexOf(num10);
			if (num11 < 0)
			{
				throw new Exception("不可能");
			}
			indexList[num11] = num10 + num4;
		}
		for (int num12 = 0; num12 < num4; num12++)
		{
			indexList[list2[num12]] = num3 + num12 - num6;
		}
		SetIndexList(Owner, indexList);
	}

	private void UpDownGroupCommandExecute(object data, bool isUp)
	{
		DynamicNameValueItemOfGroupCategory runParamConfigDecorate = data as DynamicNameValueItemOfGroupCategory;
		int num = Parent.GroupCollection.IndexOf(runParamConfigDecorate) - 1;
		if (num < 0)
		{
			return;
		}
		DynamicNameValueItemOfGroupCategory preGroupData = Parent.GroupCollection[num];
		RunParamConfigDecorate item = Collection.First((RunParamConfigDecorate p) => p.GroupName == preGroupData);
		int num2 = Collection.IndexOf(item);
		Parent.GroupCollection.Remove(runParamConfigDecorate);
		Parent.GroupCollection.Insert(num, runParamConfigDecorate);
		int num3 = num2;
		foreach (RunParamConfigDecorate item2 in Collection.Where((RunParamConfigDecorate p) => p.GroupName == runParamConfigDecorate).ToList())
		{
			Collection.Remove(item2);
			Collection.Insert(num3, item2);
			num3++;
		}
		List<RunParamConfigDecorate> list = Collection.Where((RunParamConfigDecorate p) => p.GroupName == preGroupData).ToList();
		foreach (RunParamConfigDecorate item3 in list)
		{
			Collection.Remove(item3);
		}
		foreach (RunParamConfigDecorate item4 in list)
		{
			Collection.Insert(num3, item4);
			num3++;
		}
		int num4 = -1;
		int num5 = -1;
		int num6 = -1;
		int num7 = -1;
		DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup = preGroupData.collection.FirstOrDefault();
		if (dynamicNameValueItemOfGroup != null)
		{
			num4 = Parent.Collection.IndexOf(dynamicNameValueItemOfGroup);
			num5 = preGroupData.collection.Count;
			num6 = Parent.Collection.IndexOf(runParamConfigDecorate.collection.FirstOrDefault());
			num7 = runParamConfigDecorate.collection.Count;
		}
		if (preGroupData.collection.Count > 0)
		{
			int num8 = Parent.Collection.IndexOf(Parent.Collection.First((DynamicNameValueItemOfGroup p) => p == preGroupData.collection.First()));
			foreach (DynamicNameValueItemOfGroup item5 in runParamConfigDecorate.Collection)
			{
				Parent.Collection.Remove(item5);
				Parent.Collection.Insert(num8, item5);
				num8++;
			}
		}
		List<string> groupNames = Parent.GroupCollection.Select((DynamicNameValueItemOfGroupCategory p) => p.Name).ToList();
		List<int> groupIDs = Parent.GroupCollection.Select((DynamicNameValueItemOfGroupCategory p) => p.ID).ToList();
		ChangeGroupInfosToSDK(groupNames, groupIDs);
		if (dynamicNameValueItemOfGroup == null)
		{
			return;
		}
		List<int> indexList = GetIndexList(Owner);
		List<int> list2 = new List<int>(num5);
		for (int num9 = num4; num9 < num4 + num5; num9++)
		{
			int num10 = indexList.IndexOf(num9);
			if (num10 < 0)
			{
				throw new Exception("不可能");
			}
			list2.Add(num10);
		}
		for (int num11 = num6; num11 < num6 + num7; num11++)
		{
			int num12 = indexList.IndexOf(num11);
			if (num12 < 0)
			{
				throw new Exception("不可能");
			}
			indexList[num12] = num11 - num5;
		}
		for (int num13 = 0; num13 < num5; num13++)
		{
			indexList[list2[num13]] = num4 + num13 + num7;
		}
		SetIndexList(Owner, indexList);
	}

	private bool CanUpGroupCommandExecute(object data)
	{
		DynamicNameValueItemOfGroupCategory item = data as DynamicNameValueItemOfGroupCategory;
		return Parent.GroupCollection.IndexOf(item) > 0;
	}

	private void UpGroupCommandExecute(object data)
	{
		UpDownGroupCommandExecute(data, isUp: true);
	}

	private bool CanDownGroupCommandExecute(object data)
	{
		DynamicNameValueItemOfGroupCategory item = data as DynamicNameValueItemOfGroupCategory;
		return Parent.GroupCollection.IndexOf(item) < Parent.GroupCollection.Count - 1;
	}

	private void DownGroupCommandExecute(object data)
	{
		UpDownGroupCommandExecute2(data, isUp: false);
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
}
