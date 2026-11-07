using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using Hik.Xml;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class EnumEntry_LogicType : BaseNode, INotifyPropertyChanged
{
	public bool IsSelected;

	public bool IsEnable = true;

	public string DisplayTitle;

	public string Description;

	public string DisplayName;

	public int Value;

	public string StrValue;

	public string Name;

	public ObservableCollection<object> EnumEntrys;

	private DelegateCommand _CheckedCommand;

	public string NameA
	{
		get
		{
			return Name;
		}
		set
		{
			Name = value;
		}
	}

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
		}
	}

	public bool IsEnableA
	{
		get
		{
			return IsEnable;
		}
		set
		{
			IsEnable = value;
			OnPropertyChanged("IsEnableA");
		}
	}

	public bool IsSelectedA
	{
		get
		{
			return IsSelected;
		}
		set
		{
			IsSelected = value;
			OnPropertyChanged("IsSelectedA");
		}
	}

	public string DisplayTitleA
	{
		get
		{
			return DisplayTitle;
		}
		set
		{
			DisplayTitle = value;
		}
	}

	public string DescriptionA
	{
		get
		{
			return Description;
		}
		set
		{
			Description = value;
		}
	}

	public string DisplayNameA
	{
		get
		{
			return DisplayName;
		}
		set
		{
			DisplayName = value;
		}
	}

	public int ValueA
	{
		get
		{
			return Value;
		}
		set
		{
			Value = value;
		}
	}

	public ICommand CheckedCommand
	{
		get
		{
			if (_CheckedCommand == null)
			{
				_CheckedCommand = new DelegateCommand(CheckedCommandExecute);
			}
			return _CheckedCommand;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	public override string ToString()
	{
		return DisplayName;
	}

	private void CheckedCommandExecute()
	{
		object obj = (GetParentNode(3u) as Tab).FindBaseNodeByName("LogicItemFields");
		if (obj != null)
		{
			if (DisplayTitleA == "NOT")
			{
				((obj as ButtonSelecter_LogicItem).BridgeObjA as AndModuleDynamicIO).IsAllowAdd = false;
			}
			else
			{
				((obj as ButtonSelecter_LogicItem).BridgeObjA as AndModuleDynamicIO).IsAllowAdd = true;
			}
		}
		if (GetParentNode() is EnumerationLogicType enumerationLogicType)
		{
			enumerationLogicType.Value = Value.ToString();
			enumerationLogicType.SelectedChangedCmdExecute();
		}
	}
}
