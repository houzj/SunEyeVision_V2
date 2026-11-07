using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class ComboxEditDataAnalysis : NameValueItem
{
	public enum InputLimitType
	{
		None = 0,
		Ch = 1,
		En = 2,
		Num = 4,
		Symbol = 8
	}

	public ObservableCollection<object> EnumEntrys;

	public string CurValue;

	private bool _IsDropDownOpen;

	public int RejectType;

	private static Regex rg = new Regex("^[一-龥\b]$");

	private DelegateCommand<SelectionChangedEventArgs> _SelectedChangedCommand;

	public virtual ObservableCollection<object> EnumEntrysA
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
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public bool IsDropDownOpen
	{
		get
		{
			return _IsDropDownOpen;
		}
		set
		{
			_IsDropDownOpen = value;
			OnPropertyChanged("IsDropDownOpen");
		}
	}

	public int LengthA => 32;

	public int RejectTypeA
	{
		get
		{
			return RejectType;
		}
		set
		{
			RejectType = value;
		}
	}

	public override string Value
	{
		get
		{
			string text = CurValueA;
			if (CurValueA.Contains("\\r"))
			{
				text = text.Replace("\\r", "\r");
			}
			if (CurValueA.Contains("\\n"))
			{
				text = text.Replace("\\n", "\n");
			}
			return text;
		}
		set
		{
			if (!(value == ""))
			{
				CurValueA = value;
				if (value.Contains("\\r"))
				{
					value = value.Replace("\\r", "\r");
				}
				if (value.Contains("\\n"))
				{
					value = value.Replace("\\n", "\n");
				}
			}
		}
	}

	public virtual string CurValueA
	{
		get
		{
			string text = CurValue;
			if (!CurValue.Contains("\\r") && CurValue.Contains("\r"))
			{
				text = text.Replace("\r", "\\r");
			}
			if (!CurValue.Contains("\\n") && CurValue.Contains("\n"))
			{
				text = text.Replace("\n", "\\n");
			}
			return text;
		}
		set
		{
			if (!(CurValue != value))
			{
				return;
			}
			if (value.Length > 32)
			{
				CurValue = value.Substring(0, 32);
			}
			else
			{
				if (!CheckInput(value))
				{
					return;
				}
				CurValue = value;
			}
			if (OldValue != Value && SetParamUnit() >= 0)
			{
				OldValue = Value;
			}
			OnPropertyChanged("CurValueA");
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand<SelectionChangedEventArgs>((Action<SelectionChangedEventArgs>)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
	}

	public void SelectedChangedCmdExecute(SelectionChangedEventArgs e)
	{
		IList addedItems = e.AddedItems;
		if (addedItems.Count > 0 && addedItems[0] != null)
		{
			EnumEntryStr enumEntryStr = addedItems[0] as EnumEntryStr;
			if (!string.IsNullOrEmpty(enumEntryStr.Value))
			{
				Value = enumEntryStr.Value;
			}
		}
		IsDropDownOpen = false;
	}

	public bool CheckInput(string input)
	{
		if (RejectTypeA == 1)
		{
			input = input.TrimStart(CurValue.ToArray());
			if (!string.IsNullOrEmpty(input))
			{
				for (int i = 0; i <= input.Length - 1; i++)
				{
					if (rg.IsMatch(input.Substring(i, 1)))
					{
						HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ComboInputTypeError"));
						return false;
					}
				}
			}
		}
		return true;
	}
}
