using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.BaseData;
using Apps.Log;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.XmlData;
using Hik.Xml;
using HikExternalCall.Common;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class GroupRunParam : GroupParam, IChildren, IResetConfig
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
						else
						{
							GroupRunParamConfigData.DelUserDataByName(Owner, item2.Key, allDynamicParamUserData.Keys.OfType<string>().ToList());
						}
					}
				}
				groupCollection = new ObservableCollection<DynamicNameValueItemOfGroupCategory>();
				List<string> groupNames = GetGroupNames(Owner);
				List<int> groupIDs = GetGroupIDs(Owner);
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

	internal static List<string> GetGroupNames(VmModuleSet vmModuleSet)
	{
		byte[] res = vmModuleSet.UserData["ParamGroupNamesList"];
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

	internal static List<int> GetGroupIDs(VmModuleSet vmModuleSet)
	{
		byte[] res = vmModuleSet.UserData["ParamGroupIDsList"];
		List<string> groupInfos = GetGroupInfos(res);
		return groupInfos.Select((string p) => int.Parse(p)).ToList();
	}

	private Dictionary<string, NameValueItem> GetAllDynamicParamUserData(VmModuleSet vmModuleSet, List<string> tempDic)
	{
		try
		{
			Dictionary<string, NameValueItem> dictionary = new Dictionary<string, NameValueItem>();
			string key = $"{VmModuleSet.IndexListName}";
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
			LogHelper.Error($"GetAllDynamicParamUserData Error:{ex.Message}");
			HikMessageBox.Show(ex.Message, MessageButtons.NoButton, MessageIcons.Error);
			return null;
		}
	}

	internal static NameValueItem LoadNameValueItemByName(VmModuleSet vmModuleSet, string name)
	{
		string key = $"{VmModuleSet.HeadValuePre}{name}";
		string result = StringUtil.UTF8GetString(vmModuleSet.UserData[key]);
		string[] pathInfo = RunParamConfigDecorate.GetPathInfo(name, vmModuleSet);
		string text = pathInfo[0];
		if (!((IVmSolution)VmSolution.Instance).IsModuleTypeLoaded(text))
		{
			VmModuleSet.LoadModuleDLL(text);
		}
		return BaseNode.LoadXml(result) as NameValueItem;
	}

	private void ConfigCommandExecute(DialogHandler method)
	{
		GroupRunParamConfigData groupRunParamConfigData = new GroupRunParamConfigData();
		groupRunParamConfigData.Owner = Owner;
		groupRunParamConfigData.Parent = this;
		groupRunParamConfigData.Collection = new ObservableCollection<RunParamConfigDecorate>();
		foreach (DynamicNameValueItemOfGroupCategory item4 in GroupCollection)
		{
			groupRunParamConfigData.Collection.Add(GroupRunParamConfigData.GetRunParamConfigDecorateForGroup(item4));
		}
		foreach (DynamicNameValueItemOfGroup item in Collection)
		{
			RunParamConfigDecorate item2 = groupRunParamConfigData.Collection.LastOrDefault((RunParamConfigDecorate p) => p.GroupName == item.Group);
			int index = groupRunParamConfigData.Collection.IndexOf(item2);
			groupRunParamConfigData.Collection.Insert(index, new RunParamConfigDecorate
			{
				DecorateSource = item,
				Owner = Owner
			});
		}
		Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: true);
		method.Invoke((object)groupRunParamConfigData);
		Ioc.Get<IUnRedoService>()?.SetForbidRecord(isForbid: false);
		collection = null;
		groupCollection = null;
		OnPropertyChanged("Collection");
		OnPropertyChanged("GroupCollection");
		GetParamUnit();
		IEnumerable<DynamicNameValueItemOfGroupCategory> enumerable = groupRunParamConfigData.Collection.Select((RunParamConfigDecorate p) => p.GroupName).Distinct();
		foreach (DynamicNameValueItemOfGroupCategory item3 in enumerable)
		{
			DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = GroupCollection.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.ID == item3.ID);
			if (dynamicNameValueItemOfGroupCategory != null)
			{
				dynamicNameValueItemOfGroupCategory.IsExpanded = item3.IsExpanded;
			}
		}
	}

	public override int GetParamUnit()
	{
		foreach (DynamicNameValueItemOfGroup item in Collection)
		{
			if (item.IsRelationEnable)
			{
				item.CloneSource.GetParamUnit();
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
		if (param is DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup)
		{
			firstParamRelationModule = Owner.GetFirstParamRelationModule(dynamicNameValueItemOfGroup.NameA);
			if (dynamicNameValueItemOfGroup.CloneSource is ROISelecter)
			{
				VmIO displayImage = ((firstParamRelationModule != null) ? firstParamRelationModule.Inputs.OfType<VmIO>().FirstOrDefault((VmIO p) => p.TypeName == "IMAGE") : null);
				IModuleResultView moduleResultView = Ioc.Get<IModuleResultView>();
				if (moduleResultView != null)
				{
					moduleResultView.SetDisplayImage((object)displayImage);
					moduleResultView.SetRoiVisible((object)dynamicNameValueItemOfGroup.CloneSource, dynamicNameValueItemOfGroup.IsROIVisible);
				}
			}
		}
		SelectedModule = firstParamRelationModule;
	}

	void IResetConfig.ResetModule()
	{
		if (selectedModule != Owner && selectedModule != null)
		{
			selectedModule = Owner;
		}
	}
}
