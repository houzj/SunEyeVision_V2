using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;

namespace DynamicObjectConfigControl.XmlData;

public class DynamicNameValueItemOfGroupCategory : NotifyPropertyChangedBase
{
	public IRunParam Parent;

	public List<DynamicNameValueItemOfGroup> collection = new List<DynamicNameValueItemOfGroup>();

	private string _Name;

	private bool isSelected;

	private bool isExpanded;

	private ICommand _SelectCommand = null;

	private DelegateCommand<string> _TextChangedCommand = null;

	internal int ID;

	public List<DynamicNameValueItemOfGroup> Collection => collection;

	public string Name
	{
		get
		{
			return _Name;
		}
		set
		{
			if (_Name != value)
			{
				if (CheckName(value))
				{
					_Name = value;
				}
				((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
			}
		}
	}

	public bool IsSelected
	{
		get
		{
			return isSelected;
		}
		set
		{
			if (isSelected != value)
			{
				isSelected = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsSelected");
			}
		}
	}

	public bool IsExpanded
	{
		get
		{
			return isExpanded;
		}
		set
		{
			if (isExpanded != value)
			{
				isExpanded = value;
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsExpanded");
			}
		}
	}

	public ICommand SelectCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _SelectCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)SelectCommandExecute);
				ICommand command2 = (ICommand)val;
				_SelectCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedExecute)));

	internal void SelectCommandExecute()
	{
		if (!IsSelected)
		{
			foreach (DynamicNameValueItemOfGroupCategory item in Parent.GroupCollection)
			{
				item.IsSelected = false;
			}
			IsSelected = true;
		}
		else if (Parent is GlobalParamConfigItem globalParamConfigItem)
		{
			globalParamConfigItem.SelectedItem = null;
		}
	}

	private void TextChangedExecute(string newName)
	{
		string name = _Name;
		Name = newName;
		if (name != Name)
		{
			List<string> groupNames = GroupRunParam.GetGroupNames(Parent.Owner, Parent.GroupNamesKey);
			int index = groupNames.IndexOf(name);
			groupNames[index] = _Name;
			ChangeGroupNamesToSDK(Parent.Owner, groupNames);
		}
	}

	private void ChangeGroupNamesToSDK(VmModuleSet vmModuleSet, List<string> groupNames)
	{
		if (groupNames == null)
		{
			vmModuleSet.UserData[Parent.GroupNamesKey] = null;
			return;
		}
		string text = string.Join(";", groupNames);
		vmModuleSet.UserData[Parent.GroupNamesKey] = (string.IsNullOrEmpty(text) ? null : StringUtil.UTF8GetBytesPadZero(text));
	}

	private bool CheckName(string newName)
	{
		if (!VmIODecorate.CommonNameCheck(newName))
		{
			return false;
		}
		if (Parent.GroupCollection.Any((DynamicNameValueItemOfGroupCategory p) => p != this && p.Name == newName))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameExited", MessageIcons.Error, isTranslate: true);
			return false;
		}
		return true;
	}

	public override string ToString()
	{
		return Name;
	}
}
