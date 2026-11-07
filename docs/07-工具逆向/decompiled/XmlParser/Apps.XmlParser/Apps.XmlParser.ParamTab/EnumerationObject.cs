using System;
using System.Collections;
using System.Windows;
using System.Windows.Input;
using Apps.Data;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class EnumerationObject : NameValueItem
{
	public string strChoice = ((string)Application.Current.TryFindResource("Lang_PlzChoose")) ?? "请选择";

	public IList EnumEntrys;

	public int CurValue;

	public int DefaultValue;

	public string NozzleIndexTrigger;

	public string EnumName;

	public string DeleteCommandName;

	public string AddCommandName;

	public string EditCommandName;

	private DelegateCommand _SelectedChangedCommand = null;

	public virtual IList EnumEntrysA
	{
		get
		{
			if (!string.IsNullOrEmpty(EnumName))
			{
				return EnumEntrys = (IList)Operation.GetPropertyValueByName(EnumName);
			}
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

	public virtual int CurValueA
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
			OnPropertyChanged("DefaultValueA");
		}
	}

	public string NozzleIndexTriggerA
	{
		get
		{
			return NozzleIndexTrigger;
		}
		set
		{
			NozzleIndexTrigger = value;
			OnPropertyChanged("NozzleIndexTriggerA");
		}
	}

	public string DeleteCommandNameA
	{
		get
		{
			return DeleteCommandName;
		}
		set
		{
			DeleteCommandName = value;
			OnPropertyChanged("DeleteCommandNameA");
		}
	}

	public string AddCommandNameA
	{
		get
		{
			return AddCommandName;
		}
		set
		{
			AddCommandName = value;
			OnPropertyChanged(" AddCommandNameA");
		}
	}

	public string EditCommandNameA
	{
		get
		{
			return EditCommandName;
		}
		set
		{
			EditCommandName = value;
			OnPropertyChanged(" EditCommandNameA");
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
			if (!string.IsNullOrWhiteSpace(value))
			{
				CurValueA = int.Parse(value);
			}
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
		}
		set
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected Obj, but got Unknown
			_SelectedChangedCommand = (DelegateCommand)value;
		}
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

	public override void GetParamValue()
	{
		MyItemState = ItemState.Normal;
		try
		{
			if (!string.IsNullOrEmpty(EnumName))
			{
				EnumEntrysA = (IList)Operation.GetPropertyValueByName(EnumName);
			}
			CurValueA = (int)Operation.GetPropertyValueByName(Name);
		}
		catch (Exception ex)
		{
			MyItemState = ItemState.GetParamErro;
			OnGetValueCompleted(success: false);
			string text = ((string)Application.Current.TryFindResource("Lang_ParamGet")) ?? "（获取参数）";
			if (IsGetShowError)
			{
				HikMessageBox.Show(text + ex.ToString(), MessageIcons.Error);
			}
		}
	}
}
