using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.BaseData;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.XmlData;
using DynamicObjectConfigControl.XmlData.Extern;
using Hik.Xml;
using HikExternalCall.Common;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GroupRunParam : GroupParam, IChildren, IResetConfig, IRunParam
{
	private const string _DefaultStartName = "in";

	public const string ParamGroupNamesList = "ParamGroupNamesList";

	public const string ParamGroupIDsList = "ParamGroupIDsList";

	private ObservableCollection<DynamicNameValueItemOfGroupCategory> groupCollection;

	private ObservableCollection<DynamicNameValueItemOfGroup> collection;

	private ICommand _ConfigCommand = null;

	private IVmModule selectedModule = null;

	private DelegateCommand<object> selectedCommand = null;

	public override VmIOCollection TargetVmIOs => Owner?.Inputs;

	public override string DefaultStartName => "in";

	public override string CurrentParamName => "DynamicInputParam";

	ObservableCollection<DynamicNameValueItemOfGroupCategory> IRunParam.GroupCollection => GroupCollection;

	ObservableCollection<DynamicNameValueItemOfGroup> IRunParam.Collections => Collection;

	VmModuleSet IRunParam.Owner => Owner;

	string IRunParam.GroupNamesKey => "ParamGroupNamesList";

	string IRunParam.GroupIDsKey => "ParamGroupIDsList";

	string IRunParam.ParamIndexListKey => VmModuleSet.IndexListName;

	public ObservableCollection<DynamicNameValueItemOfGroupCategory> GroupCollection
	{
		get
		{
			if (groupCollection == null)
			{
				ObservableCollection<DynamicNameValueItemOfGroup> observableCollection = Collection;
			}
			return groupCollection;
		}
	}

	public ObservableCollection<DynamicNameValueItemOfGroup> Collection
	{
		get
		{
			if (collection == null)
			{
				collection = new ObservableCollection<DynamicNameValueItemOfGroup>();
				List<string> allDynamicParam = ((IVmModuleSet)Owner).GetAllDynamicParam();
				Dictionary<string, NameValueItem> allDynamicParamUserData = GetAllDynamicParamUserData(Owner, allDynamicParam);
				if (allDynamicParamUserData != null)
				{
					foreach (KeyValuePair<string, NameValueItem> item2 in allDynamicParamUserData)
					{
						if (allDynamicParam.Contains(item2.Key))
						{
							DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup = new DynamicNameValueItemOfGroup
							{
								Name = item2.Key,
								CloneSource = allDynamicParamUserData[item2.Key]
							};
							dynamicNameValueItemOfGroup.SetParentNode(this);
							dynamicNameValueItemOfGroup.CloneSource.Name = item2.Key;
							dynamicNameValueItemOfGroup.CloneSource.DisplayName = item2.Key;
							dynamicNameValueItemOfGroup.CloneSource.SetParentNode(dynamicNameValueItemOfGroup);
							collection.Add(dynamicNameValueItemOfGroup);
						}
					}
				}
				groupCollection = new ObservableCollection<DynamicNameValueItemOfGroupCategory>();
				List<string> groupNames = GetGroupNames(Owner, "ParamGroupNamesList");
				List<int> groupIDs = GetGroupIDs(Owner, "ParamGroupIDsList");
				for (int i = 0; i < groupNames.Count; i++)
				{
					DynamicNameValueItemOfGroupCategory item = new DynamicNameValueItemOfGroupCategory
					{
						Parent = this,
						Name = groupNames[i],
						ID = groupIDs[i]
					};
					groupCollection.Add(item);
				}
				if (groupCollection.Count > 0)
				{
					groupCollection.First().IsSelected = true;
				}
				foreach (DynamicNameValueItemOfGroup item3 in collection)
				{
					int groupID = item3.GetGroupID();
					DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = groupCollection.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.ID == groupID);
					if (dynamicNameValueItemOfGroupCategory != null)
					{
						dynamicNameValueItemOfGroupCategory.collection.Add(item3);
						item3.Group = dynamicNameValueItemOfGroupCategory;
					}
				}
			}
			return collection;
		}
	}

	IList IChildren.Children => Collection;

	public ICommand ConfigCommand => _ConfigCommand ?? (_ConfigCommand = (ICommand)new DelegateCommand<DialogHandler>((Action<DialogHandler>)ConfigCommandExecute));

	public IVmModule SelectedModule
	{
		get
		{
			if (selectedModule == null)
			{
				selectedModule = Owner;
			}
			return selectedModule;
		}
		set
		{
			if (selectedModule != value)
			{
				selectedModule = value;
			}
			OnPropertyChanged("SelectedModule");
		}
	}

	public ICommand SelectedCommand
	{
		get
		{
			if (selectedCommand == null)
			{
				selectedCommand = new DelegateCommand<object>((Action<object>)SelectedCommandExecute);
			}
			return (ICommand)selectedCommand;
		}
	}

	public event UpdateDecorateDelegate UpdateDecorateEvent;

	internal static List<string> GetGroupNames(VmModuleSet vmModuleSet, string dataKey)
	{
		byte[] res = vmModuleSet.UserData[dataKey];
		return GetGroupInfos(res);
	}

	private static List<string> GetGroupInfos(byte[] res)
	{
		List<string> result = new List<string>();
		string text = null;
		if (res != null)
		{
			text = StringUtil.UTF8GetString(res);
			result = text.Split(';').ToList();
		}
		return result;
	}

	internal static List<int> GetGroupIDs(VmModuleSet vmModuleSet, string dataKey)
	{
		byte[] res = vmModuleSet.UserData[dataKey];
		List<string> groupInfos = GetGroupInfos(res);
		return groupInfos.Select((string p) => int.Parse(p)).ToList();
	}

	private Dictionary<string, NameValueItem> GetAllDynamicParamUserData(VmModuleSet vmModuleSet, List<string> tempDic)
	{
		try
		{
			Dictionary<string, NameValueItem> dictionary = new Dictionary<string, NameValueItem>();
			string key = VmModuleSet.IndexListName ?? "";
			List<int> list = ExternalCallHelper.BytesToStructs<int>(Owner.UserData[key]);
			string[] array = new string[tempDic.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[list[i]] = tempDic[i];
			}
			string[] array2 = array;
			foreach (string text in array2)
			{
				NameValueItem value = LoadNameValueItemByName(vmModuleSet, text);
				dictionary.Add(text, value);
			}
			return dictionary;
		}
		catch (Exception ex)
		{
			LogHelper.Error("GetAllDynamicParamUserData Error:" + ex.Message);
			HikMessageBox.Show(ex.Message, MessageButtons.NoButton, MessageIcons.Error);
			return null;
		}
	}

	internal static NameValueItem LoadNameValueItemByName(VmModuleSet vmModuleSet, string name)
	{
		string key = VmModuleSet.HeadValuePre + name;
		byte[] array = vmModuleSet.UserData[key];
		if (array != null)
		{
			string result = StringUtil.UTF8GetString(array);
			string[] pathInfo = RunParamConfigDecorate.GetPathInfo(name, vmModuleSet);
			string text = pathInfo[0];
			if (!((IVmSolution)VmSolution.Instance).IsModuleTypeLoaded(text))
			{
				VmModuleSet.LoadModuleDLL(text);
			}
			return BaseNode.LoadXml(result) as NameValueItem;
		}
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.DeleteDynamicParam(vmModuleSet.ID, name);
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			string message = "DeleteDynamicParam error: " + name + ", " + AppLocalizationService.GetLang("errCode:") + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(errorCode);
			LogHelper.Error(message);
		}
		return null;
	}

	private void ConfigCommandExecute(DialogHandler method)
	{
		GroupRunParamConfigData groupRunParamConfigData = new GroupRunParamConfigData();
		groupRunParamConfigData.Owner = Owner;
		groupRunParamConfigData.Parent = this;
		groupRunParamConfigData.Collection = new ObservableCollection<RunParamConfigDecorate>();
		foreach (DynamicNameValueItemOfGroupCategory item3 in GroupCollection)
		{
			groupRunParamConfigData.Collection.Add(ParamConfigDataBase.GetRunParamConfigDecorateForGroup(item3));
		}
		List<DynamicNameValueItemOfGroup> list = new List<DynamicNameValueItemOfGroup>();
		foreach (DynamicNameValueItemOfGroup item in Collection)
		{
			RunParamConfigDecorate runParamConfigDecorate = groupRunParamConfigData.Collection.LastOrDefault((RunParamConfigDecorate p) => p.GroupName == item.Group);
			if (runParamConfigDecorate != null)
			{
				int index = groupRunParamConfigData.Collection.IndexOf(runParamConfigDecorate);
				groupRunParamConfigData.Collection.Insert(index, new RunParamConfigDecorate
				{
					DecorateSource = item,
					Owner = Owner
				});
			}
			else
			{
				list.Add(item);
			}
		}
		list.ForEach((DynamicNameValueItemOfGroup q) =>
		{
			groupRunParamConfigData.DeleteParam(new RunParamConfigDecorate
			{
				DecorateSource = q,
				Owner = Owner
			});
		});
		Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: true);
		method.Invoke((object)groupRunParamConfigData);
		Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: false);
		collection = null;
		groupCollection = null;
		OnPropertyChanged("Collection");
		OnPropertyChanged("GroupCollection");
		GetParamUnit();
		IEnumerable<DynamicNameValueItemOfGroupCategory> enumerable = groupRunParamConfigData.Collection.Select((RunParamConfigDecorate p) => p.GroupName).Distinct();
		foreach (DynamicNameValueItemOfGroupCategory item2 in enumerable)
		{
			DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = GroupCollection.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.ID == item2.ID);
			if (dynamicNameValueItemOfGroupCategory != null)
			{
				dynamicNameValueItemOfGroupCategory.IsExpanded = item2.IsExpanded;
			}
		}
	}

	public override int GetParamUnit()
	{
		foreach (DynamicNameValueItemOfGroup item in Collection)
		{
			if (item.IsRelationEnable)
			{
				try
				{
					item.CloneSource.GetParamUnit();
				}
				catch (Exception ex)
				{
					LogHelper.Error(item.CloneSource.DisplayNameA + " GetParamUnit error: " + ex.Message);
				}
			}
		}
		return 0;
	}

	private void SelectedCommandExecute(object param)
	{
		if (Owner == null)
		{
			return;
		}
		IVmModule firstParamRelationModule = Owner;
		IDecorate decorate = null;
		if (param is DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup)
		{
			firstParamRelationModule = Owner.GetFirstParamRelationModule(dynamicNameValueItemOfGroup.NameA);
			if (dynamicNameValueItemOfGroup.CloneSource is IGroupRunParam param2)
			{
				decorate = param2.GetSupportDisplayForGroupData();
				if (decorate == null)
				{
					decorate = new CommonParamDecorate
					{
						DecorateSource = dynamicNameValueItemOfGroup.CloneSource
					};
				}
			}
		}
		else
		{
			decorate = new ModuleDecorate
			{
				DecorateSource = this
			};
		}
		SelectedModule = firstParamRelationModule;
		OnUpdateDecorateEvent(decorate);
	}

	void IResetConfig.ResetModule()
	{
		if (selectedModule != Owner && selectedModule != null)
		{
			selectedModule = Owner;
		}
	}

	public void OnUpdateDecorateEvent(IDecorate decorate)
	{
		if (UpdateDecorateEvent != null)
		{
			UpdateDecorateEvent(decorate);
		}
	}

	public void ClearDecorateEvent()
	{
		UpdateDecorateEvent = null;
	}
}
