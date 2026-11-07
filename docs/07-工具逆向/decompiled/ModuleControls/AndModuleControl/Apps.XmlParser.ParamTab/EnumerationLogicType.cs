using System.Collections.ObjectModel;
using System.Windows.Input;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class EnumerationLogicType : NameValueItem
{
	public ObservableCollection<object> EnumEntrys;

	public int CurValue;

	public int DefaultValue;

	private DelegateCommand _SelectedChangedCommand;

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

	public int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public int DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			if (string.IsNullOrEmpty(value))
			{
				value = CurValueA.ToString();
			}
			CurValueA = int.Parse(value);
			SetSelectedLogicType(CurValueA);
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand(SelectedChangedCmdExecute);
			}
			return _SelectedChangedCommand;
		}
	}

	public void SetSelectedLogicType(int selectIndex)
	{
		foreach (object enumEntry in EnumEntrys)
		{
			if ((enumEntry as EnumEntry_LogicType).ValueA == selectIndex)
			{
				(enumEntry as EnumEntry_LogicType).IsSelectedA = true;
			}
			else if ((enumEntry as EnumEntry_LogicType).IsSelectedA)
			{
				(enumEntry as EnumEntry_LogicType).IsSelectedA = false;
			}
		}
	}

	public object GetLogicType(string typeName)
	{
		object result = null;
		foreach (object enumEntry in EnumEntrys)
		{
			if ((enumEntry as EnumEntry_LogicType).DisplayTitleA == typeName)
			{
				result = enumEntry;
				break;
			}
		}
		return result;
	}

	public void SelectedChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			int num = SetParamUnit();
			OldValue = Value;
			if (num >= 0)
			{
				PreExecuteRun();
			}
		}
	}
}
