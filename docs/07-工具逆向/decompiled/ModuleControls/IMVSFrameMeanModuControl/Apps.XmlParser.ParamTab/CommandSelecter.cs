using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class CommandSelecter : NameValueItem
{
	public int CurValue;

	public int DefaultValue;

	public ObservableCollection<object> EnumEntrys;

	private DelegateCommand _SelectedChangedCommand;

	public int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
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

	public int LangCurrentImageWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return 150;
			}
			return 116;
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
			SetSelectedCommand(CurValueA);
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

	public void SetSelectedCommand(int selectIndex)
	{
		foreach (object enumEntry in EnumEntrys)
		{
			if ((enumEntry as Command_EnumEntry).ValueA == selectIndex)
			{
				(enumEntry as Command_EnumEntry).IsSelectedA = true;
			}
			else if ((enumEntry as Command_EnumEntry).IsSelectedA)
			{
				(enumEntry as Command_EnumEntry).IsSelectedA = false;
			}
		}
	}

	public void SelectedChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			SetParamUnit();
			OldValue = Value;
		}
	}
}
