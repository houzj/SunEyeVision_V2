using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.XmlData;
using DynamicObjectConfigControl.XmlData.Extern;
using Hik.Xml;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GlobalParamConfigItem : ParamConfigDataBase, IRunParam, IParentNode
{
	internal IGlobalData GlobalData;

	private ObservableCollection<DynamicNameValueItemOfGroupCategory> groupCollection;

	private ObservableCollection<DynamicNameValueItemOfGroup> paramCollection;

	private string _name;

	private bool _isReName;

	private bool _isSelected;

	private bool _isUnfold;

	private bool _isVisible = true;

	private DelegateCommand _RenameGlobalParamCommand = null;

	private DelegateCommand _DeleteGlobalParamCommand = null;

	private ICommand _UpCommand = null;

	private ICommand _DownCommand = null;

	private ICommand _SelectParamCommand = null;

	ObservableCollection<DynamicNameValueItemOfGroupCategory> IRunParam.GroupCollection => GroupCollection;

	ObservableCollection<DynamicNameValueItemOfGroup> IRunParam.Collections => ParamCollection;

	VmModuleSet IRunParam.Owner => VmSolution.Instance;

	string IRunParam.GroupNamesKey => $"GroupNamesList{ID}";

	string IRunParam.GroupIDsKey => $"GroupIDsList{ID}";

	string IRunParam.ParamIndexListKey => $"RunParamIndexList{ID}";

	public ObservableCollection<DynamicNameValueItemOfGroupCategory> GroupCollection
	{
		get
		{
			if (groupCollection == null)
			{
				ObservableCollection<DynamicNameValueItemOfGroup> observableCollection = ParamCollection;
			}
			return groupCollection;
		}
	}

	public ObservableCollection<DynamicNameValueItemOfGroup> ParamCollection
	{
		get
		{
			if (paramCollection == null)
			{
				paramCollection = new ObservableCollection<DynamicNameValueItemOfGroup>();
				groupCollection = new ObservableCollection<DynamicNameValueItemOfGroupCategory>();
				List<DynamicNameValueItemOfGroup> list = new List<DynamicNameValueItemOfGroup>();
				List<string> groupNames = GroupRunParam.GetGroupNames(Owner, Parent.GroupNamesKey);
				List<int> groupIDs = GroupRunParam.GetGroupIDs(Owner, Parent.GroupIDsKey);
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
				foreach (DynamicNameValueItemOfGroup item2 in GlobalData.AllParamCollection)
				{
					int groupID = item2.GetGroupID();
					DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory = groupCollection.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.ID == groupID);
					if (dynamicNameValueItemOfGroupCategory != null)
					{
						list.Add(item2);
					}
				}
				List<int> indexList = GetIndexList(Owner);
				int num = ((indexList != null && indexList.Count == list.Count) ? indexList.Count : 0);
				if (num > 0)
				{
					DynamicNameValueItemOfGroup[] array = new DynamicNameValueItemOfGroup[num];
					for (int num2 = 0; num2 < num; num2++)
					{
						if (indexList[num2] >= 0 && indexList[num2] < num)
						{
							array[indexList[num2]] = list[num2];
						}
					}
					list = array.ToList();
				}
				foreach (DynamicNameValueItemOfGroup item3 in list)
				{
					if (item3 != null)
					{
						int groupID2 = item3.GetGroupID();
						DynamicNameValueItemOfGroupCategory dynamicNameValueItemOfGroupCategory2 = groupCollection.FirstOrDefault((DynamicNameValueItemOfGroupCategory p) => p.ID == groupID2);
						if (dynamicNameValueItemOfGroupCategory2 != null)
						{
							item3.Group = dynamicNameValueItemOfGroupCategory2;
							dynamicNameValueItemOfGroupCategory2.collection.Add(item3);
							paramCollection.Add(item3);
						}
					}
				}
				foreach (DynamicNameValueItemOfGroup item4 in paramCollection)
				{
					if (item4.IsRelationEnable)
					{
						try
						{
							item4.CloneSource.GetParamUnit();
						}
						catch (Exception ex)
						{
							LogHelper.Error(item4.CloneSource.DisplayNameA + " GetParamUnit error: " + ex.Message);
						}
					}
				}
			}
			return paramCollection;
		}
	}

	public int ID { get; set; }

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			if (value != _name)
			{
				if (GlobalData != null && GlobalData.GlobalParamItems != null && GlobalData.GlobalParamItems.Any((GlobalParamConfigItem p) => p.Name == value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_RenameTip"), MessageIcons.Warning);
				}
				else if (VmIODecorate.CommonNameCheck(value))
				{
					_name = value;
					((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
				}
			}
		}
	}

	public bool IsReName
	{
		get
		{
			return _isReName;
		}
		set
		{
			if (value != _isReName)
			{
				_isReName = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsReName");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return _isSelected;
		}
		set
		{
			if (value != _isSelected)
			{
				_isSelected = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsSelected");
			}
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
			if (value != _isUnfold)
			{
				_isUnfold = value;
				Collection.Where((RunParamConfigDecorate p) => p.IsForGroup).ToList().ForEach((RunParamConfigDecorate q) =>
				{
					q.GroupName.IsExpanded = value;
				});
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsUnfold");
			}
		}
	}

	public bool IsVisible
	{
		get
		{
			return _isVisible;
		}
		set
		{
			if (value != _isVisible)
			{
				_isVisible = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsVisible");
			}
		}
	}

	public ICommand RenameGlobalParamCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _RenameGlobalParamCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)RenameGlobalParamCommandExecute);
				DelegateCommand val3 = val2;
				_RenameGlobalParamCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand DeleteGlobalParamCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _DeleteGlobalParamCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)DeleteGlobalParamCommandExecute);
				DelegateCommand val3 = val2;
				_DeleteGlobalParamCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public ICommand UpCommand
	{
		get
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Expected Obj, but got Unknown
			//IL_0030: Expected Obj, but got Unknown
			ICommand command = _UpCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)UpCommandExecute, (Func<bool>)CanUpCommandExecute, false);
				ICommand command2 = (ICommand)val;
				_UpCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand DownCommand
	{
		get
		{
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0029: Unknown result type (might be due to invalid IL or missing references)
			//IL_002b: Expected Obj, but got Unknown
			//IL_0030: Expected Obj, but got Unknown
			ICommand command = _DownCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)DownCommandExecute, (Func<bool>)CanDownCommandExecute, false);
				ICommand command2 = (ICommand)val;
				_DownCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand SelectParamCommand => _SelectParamCommand ?? (_SelectParamCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectParamCommandExecute));

	public object GetParentNode()
	{
		return GlobalData.SolutionTabRoot;
	}

	public void SetParentNode(object parentNode)
	{
		throw new NotImplementedException();
	}

	private void RenameGlobalParamCommandExecute()
	{
		IsReName = true;
	}

	private void DeleteGlobalParamCommandExecute()
	{
		if (DeleteAllParams() && GlobalData.GlobalParamItems != null && GlobalData.GlobalParamItems.Contains(this))
		{
			GlobalData.GlobalParamItems.Remove(this);
		}
	}

	private bool CanUpCommandExecute()
	{
		RunParamConfigDecorate runParamConfigDecorate = Collection.SingleOrDefault((RunParamConfigDecorate p) => p.IsForGroup && p.GroupName.IsSelected);
		if (runParamConfigDecorate == null)
		{
			return false;
		}
		if (SelectedItem is RunParamConfigDecorate { DecorateSource: not null } runParamConfigDecorate2 && runParamConfigDecorate2.GroupName.Name == runParamConfigDecorate.GroupName.Name && runParamConfigDecorate2.GroupName.IsExpanded)
		{
			return runParamConfigDecorate2.GroupName.Collection.IndexOf(runParamConfigDecorate2.DecorateSource) > 0;
		}
		return Parent.GroupCollection.IndexOf(runParamConfigDecorate.GroupName) > 0;
	}

	private void UpCommandExecute()
	{
		RunParamConfigDecorate runParamConfigDecorate = Collection.SingleOrDefault((RunParamConfigDecorate p) => p.IsForGroup && p.GroupName.IsSelected);
		if (runParamConfigDecorate != null)
		{
			if (SelectedItem is RunParamConfigDecorate { DecorateSource: not null } runParamConfigDecorate2 && runParamConfigDecorate2.GroupName.Name == runParamConfigDecorate.GroupName.Name && runParamConfigDecorate2.GroupName.IsExpanded)
			{
				UpDownParam(runParamConfigDecorate2, isUp: true);
				return;
			}
			UpGroup(runParamConfigDecorate.GroupName);
			SelectedItem = null;
		}
	}

	private bool CanDownCommandExecute()
	{
		RunParamConfigDecorate runParamConfigDecorate = Collection.SingleOrDefault((RunParamConfigDecorate p) => p.IsForGroup && p.GroupName.IsSelected);
		if (runParamConfigDecorate == null)
		{
			return false;
		}
		if (SelectedItem is RunParamConfigDecorate { DecorateSource: not null } runParamConfigDecorate2 && runParamConfigDecorate2.GroupName.Name == runParamConfigDecorate.GroupName.Name && runParamConfigDecorate2.GroupName.IsExpanded)
		{
			return runParamConfigDecorate2.GroupName.Collection.IndexOf(runParamConfigDecorate2.DecorateSource) < runParamConfigDecorate2.GroupName.Collection.Count - 1;
		}
		return Parent.GroupCollection.IndexOf(runParamConfigDecorate.GroupName) < Parent.GroupCollection.Count - 1;
	}

	private void DownCommandExecute()
	{
		RunParamConfigDecorate runParamConfigDecorate = Collection.SingleOrDefault((RunParamConfigDecorate p) => p.IsForGroup && p.GroupName.IsSelected);
		if (runParamConfigDecorate != null)
		{
			if (SelectedItem is RunParamConfigDecorate { DecorateSource: not null } runParamConfigDecorate2 && runParamConfigDecorate2.GroupName.Name == runParamConfigDecorate.GroupName.Name && runParamConfigDecorate2.GroupName.IsExpanded)
			{
				UpDownParam(runParamConfigDecorate2, isUp: false);
			}
			else
			{
				DownGroup(runParamConfigDecorate.GroupName);
			}
		}
	}

	public void SelectParamCommandExecute(object selectItem)
	{
		if (selectItem is DynamicNameValueItemOfGroup { CloneSource: IGroupRunParam cloneSource } dynamicNameValueItemOfGroup)
		{
			IDecorate decorate = cloneSource.GetSupportDisplayForGroupData();
			if (decorate == null)
			{
				decorate = new CommonParamDecorate
				{
					DecorateSource = dynamicNameValueItemOfGroup.CloneSource
				};
			}
			GlobalData.OnUpdateDecorateEvent(decorate);
		}
		else
		{
			GlobalData.OnUpdateDecorateEvent(null);
		}
	}

	public void UpdateParams()
	{
		paramCollection = null;
		groupCollection = null;
		((NotifyPropertyChangedBase)this).OnPropertyChanged("ParamCollection");
		((NotifyPropertyChangedBase)this).OnPropertyChanged("GroupCollection");
	}
}
