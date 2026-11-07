using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Apps.UIHelper.Extern;
using DynamicObjectConfigControl;
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

public class ParamConfigDataBase : NotifyPropertyChangedBase
{
	protected static int MaxRunParamsNum;

	internal IRunParam Parent;

	internal VmModuleSet Owner;

	private object selectedItem;

	internal IRelateControl RelateControl;

	private ICommand _AddGroupCommand = null;

	private ICommand _DelGroupCommand = null;

	private ICommand _DeleteParamCommand = null;

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

	public IList<RunParamConfigDecorate> Collection { get; set; } = new ObservableCollection<RunParamConfigDecorate>();

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

	public ICommand DeleteParamCommand => _DeleteParamCommand ?? (_DeleteParamCommand = (ICommand)new DelegateCommand<RunParamConfigDecorate>((Action<RunParamConfigDecorate>)DeleteParamCommandExecute));

	static ParamConfigDataBase()
	{
		MaxRunParamsNum = 32;
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

	protected void AddGroupCommandExecute()
	{
		if (Parent.GroupCollection.Count >= MaxRunParamsNum)
		{
			HikMessageBox.Show(string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_ExceedConfigNumLimit"), MaxRunParamsNum), MessageIcons.Warning);
			return;
		}
		List<string> groupNames = GroupRunParam.GetGroupNames(Owner, Parent.GroupNamesKey);
		List<int> groupIDs = GroupRunParam.GetGroupIDs(Owner, Parent.GroupIDsKey);
		string newGroupName = GetNewGroupName(groupNames);
		int num = 0;
		if (this is GlobalParamConfigItem configItem)
		{
			num = GetNewGroupID(configItem);
		}
		else if (this is GroupRunParamConfigData)
		{
			num = GetNewGroupID(groupIDs);
		}
		DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = new DynamicNameValueItemOfGroupCategory
		{
			Parent = Parent,
			Name = newGroupName,
			ID = num
		};
		Parent.GroupCollection.Add(dynamicNameValueItemOfGroupCategory);
		dynamicNameValueItemOfGroupCategory.SelectCommandExecute();
		RunParamConfigDecorate runParamConfigDecorateForGroup = GetRunParamConfigDecorateForGroup(dynamicNameValueItemOfGroupCategory);
		Collection.Add(runParamConfigDecorateForGroup);
		groupNames.Add(newGroupName);
		groupIDs.Add(num);
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

	private int GetNewGroupID(GlobalParamConfigItem configItem)
	{
		int id = 0;
		bool flag = false;
		do
		{
			foreach (GlobalParamConfigItem globalParamItem in configItem.GlobalData.GlobalParamItems)
			{
				flag = globalParamItem.GroupCollection.Any((DynamicNameValueItemOfGroupCategory p) => p.ID == id);
				if (flag)
				{
					break;
				}
			}
			if (flag)
			{
				id++;
			}
		}
		while (flag);
		return id;
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

	private void ChangeGroupInfosToSDK(List<string> groupNames, List<int> groupIDs)
	{
		if (groupNames == null || groupIDs == null)
		{
			Owner.UserData[Parent.GroupNamesKey] = null;
			Owner.UserData[Parent.GroupIDsKey] = null;
			return;
		}
		int num = 0;
		if (groupNames.Count > groupIDs.Count)
		{
			num = groupNames.Count - groupIDs.Count;
			for (int i = 0; i < num; i++)
			{
				groupNames.RemoveAt(0);
			}
		}
		else if (groupIDs.Count > groupNames.Count)
		{
			num = groupIDs.Count - groupNames.Count;
			for (int j = 0; j < num; j++)
			{
				groupIDs.RemoveAt(0);
			}
		}
		string text = string.Join(";", groupNames);
		Owner.UserData[Parent.GroupNamesKey] = (string.IsNullOrEmpty(text) ? null : StringUtil.UTF8GetBytesPadZero(text));
		string text2 = string.Join(";", groupIDs);
		Owner.UserData[Parent.GroupIDsKey] = (string.IsNullOrEmpty(text2) ? null : StringUtil.UTF8GetBytesPadZero(text2));
	}

	internal bool DeleteAllParams()
	{
		if (Collection.Count == 0)
		{
			return true;
		}
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InfoTips"), AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.YesOrNo, MessageIcons.Question) != DialogResult.Yes)
		{
			return false;
		}
		foreach (RunParamConfigDecorate item in Collection.ToList())
		{
			DeleteParamCommandExecute(item);
		}
		ChangeGroupInfosToSDK(null, null);
		Parent.GroupCollection.Clear();
		return true;
	}

	private void DelGroupCommandExecute(object data)
	{
		if (HikMessageBox.Show(AppLocalizationService.GetLang("Lang_InfoTips"), AppLocalizationService.GetLang("Lang_ConfirmDelete"), MessageButtons.YesOrNo, MessageIcons.Question) != DialogResult.Yes)
		{
			return;
		}
		DynamicNameValueItemOfGroupCategory group = data as DynamicNameValueItemOfGroupCategory;
		List<string> groupNames = GroupRunParam.GetGroupNames(Owner, Parent.GroupNamesKey);
		groupNames.Remove(group.Name);
		List<int> groupIDs = GroupRunParam.GetGroupIDs(Owner, Parent.GroupIDsKey);
		groupIDs.Remove(group.ID);
		ChangeGroupInfosToSDK(groupNames, groupIDs);
		foreach (RunParamConfigDecorate item in Collection.Where((RunParamConfigDecorate p) => p.GroupName == group).ToList())
		{
			DeleteParamCommandExecute(item);
		}
		Parent.GroupCollection.Remove(group);
	}

	protected void UpGroup(object data)
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
			num4 = Parent.Collections.IndexOf(dynamicNameValueItemOfGroup);
			num5 = preGroupData.collection.Count;
			num6 = Parent.Collections.IndexOf(runParamConfigDecorate.collection.FirstOrDefault());
			num7 = runParamConfigDecorate.collection.Count;
		}
		if (preGroupData.collection.Count > 0)
		{
			int num8 = Parent.Collections.IndexOf(Parent.Collections.First((DynamicNameValueItemOfGroup p) => p == preGroupData.collection.First()));
			foreach (DynamicNameValueItemOfGroup item5 in runParamConfigDecorate.Collection)
			{
				Parent.Collections.Remove(item5);
				Parent.Collections.Insert(num8, item5);
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

	protected void DownGroup(object data)
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
			num3 = Parent.Collections.IndexOf(dynamicNameValueItemOfGroup);
			num4 = preGroupData.collection.Count;
			num5 = Parent.Collections.IndexOf(runParamConfigDecorate.collection.FirstOrDefault());
			num6 = runParamConfigDecorate.collection.Count;
		}
		if (runParamConfigDecorate.collection.Count > 0)
		{
			int num7 = Parent.Collections.IndexOf(Parent.Collections.First((DynamicNameValueItemOfGroup p) => p == runParamConfigDecorate.collection.First()));
			foreach (DynamicNameValueItemOfGroup item5 in preGroupData.Collection)
			{
				Parent.Collections.Remove(item5);
				Parent.Collections.Insert(num7, item5);
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

	protected void UpDownParam(object data, bool isUp)
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
		int index = Parent.Collections.IndexOf(runParamConfigDecorate.DecorateSource) + ((!isUp) ? 1 : (-1));
		Parent.Collections.Remove(runParamConfigDecorate.DecorateSource);
		Parent.Collections.Insert(index, runParamConfigDecorate.DecorateSource);
		SelectedItem = data;
	}

	internal bool AddParam(NameValueItem source)
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
			InitClone(nameValueItem);
		}
		if (nameValueItem == null)
		{
			LogHelper.Error("AddParam error: newObj is null.");
			return false;
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
		if (this is GlobalParamConfigItem)
		{
			string key = VmModuleSet.GroupIDPre + text;
			VmSolution.Instance.UserData[key] = StringUtil.UTF8GetBytesPadZero(group.ID.ToString());
		}
		else
		{
			runParamConfigDecorate.SetGroupID(group.ID);
		}
		group.IsExpanded = true;
		RunParamConfigDecorate item = Collection.Last((RunParamConfigDecorate p) => p.GroupName == group);
		int index = Collection.IndexOf(item);
		Collection.Insert(index, runParamConfigDecorate);
		DynamicNameValueItemOfGroup item2 = group.Collection.LastOrDefault();
		group.collection.Add(runParamConfigDecorate.DecorateSource);
		ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParam(Owner.ID, text);
		byte[] value = StringUtil.UTF8GetBytesPadZero(nameValueItem.ToXmlString(IsShort: true));
		List<int> indexList = GetIndexList(Owner);
		int num = Parent.Collections.IndexOf(item2);
		if (num < 0)
		{
			item2 = Parent.Collections.LastOrDefault((DynamicNameValueItemOfGroup p) => Parent.GroupCollection.IndexOf(p.Group) < Parent.GroupCollection.IndexOf(group));
			num = ((item2 != null) ? Parent.Collections.IndexOf(item2) : (-1));
		}
		for (int num2 = 0; num2 < indexList.Count; num2++)
		{
			if (indexList[num2] > num)
			{
				indexList[num2]++;
			}
		}
		indexList.Add(num + 1);
		Parent.Collections.Insert(num + 1, runParamConfigDecorate.DecorateSource);
		SetIndexList(Owner, indexList);
		string key2 = VmModuleSet.HeadValuePre + text;
		Owner.UserData[key2] = value;
		string text2 = VmModuleSet.HeadPathPre + text;
		if (source.ParamsSource is VmModuleSet vmModuleSet)
		{
			byte[] bt = vmModuleSet.UserData[VmModuleSet.HeadPathPre + source.Name];
			Owner.UserData[text2] = StringUtil.UTF8GetBytesPadZero(StringUtil.UTF8GetString(bt) + "." + vmModuleSet.ID);
		}
		else if (source.ParamsSource is VmModule vmModule)
		{
			Owner.UserData[text2] = StringUtil.UTF8GetBytesPadZero(vmModule.StrModuleName + "." + source.Name + "." + source.DisplayName + "." + vmModule.ID);
			vmModule.UserData[VmModuleSet.HeadPathPre + source.DisplayName] = StringUtil.UTF8GetBytesPadZero(vmModule.ID + vmModule.StrModuleName + "." + source.DisplayName);
		}
		else
		{
			LogHelper.Error("Save param path userdata error, path key: " + text2);
		}
		ServerSDKManager.serverSDKManager.mModuleManager.AddDynamicParamRelation(Owner.ID, text, (source.ParamsSource as IVmModule).ID, source.Name);
		return true;
	}

	private void InitClone(NameValueItem newObj)
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
		if (this is GlobalParamConfigItem globalParamConfigItem)
		{
			bool flag = false;
			do
			{
				foreach (GlobalParamConfigItem globalParamItem in globalParamConfigItem.GlobalData.GlobalParamItems)
				{
					flag = globalParamItem.Collection.Any((RunParamConfigDecorate p) => !p.IsForGroup && p.Name == AppLocalizationService.UnDoTranslate(preName));
					if (flag)
					{
						break;
					}
				}
				if (flag)
				{
					preName = $"{displayNameA}{num}";
					num++;
				}
			}
			while (flag);
		}
		else
		{
			while (Collection.Any((RunParamConfigDecorate p) => !p.IsForGroup && p.Name == AppLocalizationService.UnDoTranslate(preName)))
			{
				preName = $"{displayNameA}{num}";
				num++;
			}
		}
		return preName;
	}

	public void DeleteParamCommandExecute(RunParamConfigDecorate runParamConfigDecorate)
	{
		string strPath = (runParamConfigDecorate.IsForGroup ? "" : runParamConfigDecorate.UniquePath);
		if (!DeleteParam(runParamConfigDecorate))
		{
			return;
		}
		bool flag = false;
		IList list = null;
		if (this is GlobalParamConfigItem globalParamConfigItem)
		{
			list = globalParamConfigItem.GlobalData.EnumEntrys;
		}
		else if (this is GroupRunParamConfigData groupRunParamConfigData)
		{
			list = groupRunParamConfigData.EnumEntrysA;
		}
		if (list == null)
		{
			LogHelper.Error("DeleteParamCommandExecute error: enumEntrys is null.");
			return;
		}
		foreach (Holder item in list)
		{
			foreach (Holder item2 in item.EnumEntrysA)
			{
				if (item2 is GroupHolder)
				{
					Holder holder3 = item2.EnumEntrysA.OfType<Holder>().FirstOrDefault((Holder p) => CheckParam(p.HolderTarget, strPath, delete: true));
					if (holder3 != null)
					{
						holder3.CheckEnabled = false;
						flag = true;
						break;
					}
				}
				else if (CheckParam(item2.HolderTarget, strPath, delete: true))
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

	internal bool DeleteParam(RunParamConfigDecorate runParamConfigDecorate)
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
		Parent.Collections.Remove(runParamConfigDecorate.DecorateSource);
		runParamConfigDecorate.GroupName?.Collection.Remove(runParamConfigDecorate.DecorateSource);
		return true;
	}

	internal void DelUserDataByName(VmModule vmModule, string name, IList<string> names)
	{
		int num = names.IndexOf(name);
		List<int> indexList = GetIndexList(vmModule);
		if (num < 0)
		{
			num = indexList.Count - 1;
		}
		indexList.Remove(num);
		for (int i = 0; i < indexList.Count; i++)
		{
			if (indexList[i] > num)
			{
				indexList[i]--;
			}
		}
		SetIndexList(vmModule, indexList);
		string key = VmModuleSet.HeadValuePre + name;
		vmModule.UserData[key] = null;
		string key2 = VmModuleSet.HeadPathPre + name;
		vmModule.UserData[key2] = null;
		string key3 = VmModuleSet.GroupIDPre + name;
		vmModule.UserData[key3] = null;
	}

	internal List<int> GetIndexList(VmModule vmModule)
	{
		return ExternalCallHelper.BytesToStructs<int>(vmModule.UserData[Parent.ParamIndexListKey]) ?? new List<int>();
	}

	private void SetIndexList(VmModule vmModule, List<int> ls)
	{
		vmModule.UserData[Parent.ParamIndexListKey] = ExternalCallHelper.StructsToBytes(ls);
	}

	public static bool CheckParam(object param, string strPath, bool delete = false)
	{
		bool result = false;
		if (param != null && !string.IsNullOrEmpty(strPath) && param is NameValueItem { ParamsSource: VmModule paramsSource } nameValueItem)
		{
			string text = "";
			if (paramsSource is VmModuleSet)
			{
				text = paramsSource.ID + "." + RunParamConfigDecorate.GetUniquePath(nameValueItem.NameA, paramsSource as VmModuleSet);
			}
			else
			{
				byte[] array = paramsSource.UserData[VmModuleSet.HeadPathPre + nameValueItem.DisplayName];
				if (array != null)
				{
					text = StringUtil.UTF8GetString(array);
				}
			}
			if (text.Equals(strPath))
			{
				result = true;
				if (delete && !(paramsSource is VmModuleSet))
				{
					paramsSource.UserData[VmModuleSet.HeadPathPre + nameValueItem.DisplayName] = null;
				}
			}
		}
		return result;
	}
}
