using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;
using VM.Core;

namespace GlobalCameraModuleControl.XmlData;

public class DynamicCameraPanelList : NotifyPropertyChangedBase, ICameraDynamicParam
{
	private ObservableCollection<DrowPopupObject> _ConboxDown = new ObservableCollection<DrowPopupObject>();

	private ICommand _AddGroupCommand = null;

	private ObservableCollection<DynamicCameraNameItemOfGroupCategory> groupCollection = new ObservableCollection<DynamicCameraNameItemOfGroupCategory>();

	private ObservableCollection<DynamicCameraNameItemOfGroup> collection = new ObservableCollection<DynamicCameraNameItemOfGroup>();

	public ObservableCollection<DrowPopupObject> ConboxDown
	{
		get
		{
			return _ConboxDown;
		}
		set
		{
			if (_ConboxDown != null)
			{
				_ConboxDown = value;
			}
		}
	}

	ObservableCollection<DynamicCameraNameItemOfGroupCategory> ICameraDynamicParam.GroupCollection => GroupCollection;

	ObservableCollection<DynamicCameraNameItemOfGroup> ICameraDynamicParam.Collections => Collection;

	VmModule ICameraDynamicParam.Owner => Owner;

	public VmModule Owner { get; set; }

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

	public ObservableCollection<DynamicCameraNameItemOfGroupCategory> GroupCollection
	{
		get
		{
			return groupCollection;
		}
		set
		{
			if (groupCollection != value)
			{
				groupCollection = value;
			}
		}
	}

	public ObservableCollection<DynamicCameraNameItemOfGroup> Collection
	{
		get
		{
			return collection;
		}
		set
		{
			if (collection != value)
			{
				collection = value;
			}
		}
	}

	protected void AddGroupCommandExecute()
	{
		List<string> hasNamed = GroupCollection.Select((DynamicCameraNameItemOfGroupCategory x) => x.Name).ToList();
		string name = GenerateUniqueName(hasNamed, 0);
		DynamicCameraNameItemOfGroupCategory item = new DynamicCameraNameItemOfGroupCategory
		{
			Name = name,
			Parent = this
		};
		GroupCollection.Add(item);
	}

	private string GenerateUniqueName(List<string> HasNamed, int i)
	{
		string text = AppLocalizationService.GetLang("默认分组") + i;
		if (!HasNamed.Contains(text))
		{
			return text;
		}
		i++;
		return GenerateUniqueName(HasNamed, i);
	}

	public void ModifyGroupByName(DynamicCameraNameItemOfGroupCategory name)
	{
		List<DynamicCameraNameItemOfGroup> list = Collection.Where((DynamicCameraNameItemOfGroup X) => X.CheckEnabled == true).ToList();
		foreach (DynamicCameraNameItemOfGroup item in list)
		{
			try
			{
				item.Owner.Params["ModifyParamGroupName"] = item.NameA + ";" + name.Name;
			}
			catch
			{
			}
			item.Group.Collection.Remove(item);
			item.Group.OnPropertyChanged("CheckEnabled");
			item.TreeGroupName = name.Name;
			name.Collection.Add(item);
			item.Group = name;
			item.Group.OnPropertyChanged("CheckEnabled");
		}
	}
}
