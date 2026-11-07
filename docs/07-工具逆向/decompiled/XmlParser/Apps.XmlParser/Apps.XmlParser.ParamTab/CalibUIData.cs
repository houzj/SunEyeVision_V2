using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Apps.Data;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class CalibUIData : NameValueItem
{
	public string strChoice = ((string)Application.Current.TryFindResource("Lang_PlzChoose")) ?? "请选择";

	public string CurValue;

	public string DefaultValue;

	public ObservableCollection<object> EnumEntrys;

	public object CalibMat;

	private DelegateCommand _SelectedChangedCommand = null;

	public bool IsReadOnlyFlag = true;

	public bool IsEditable = true;

	public virtual ObservableCollection<object> EnumEntrysA
	{
		get
		{
			if (!string.IsNullOrEmpty(EnumName))
			{
				return EnumEntrys = (ObservableCollection<object>)Operation.GetPropertyValueByName(EnumName);
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

	public virtual object CalibMatA
	{
		get
		{
			if (!string.IsNullOrEmpty(MatEnumNameA))
			{
				return CalibMat = Operation.GetPropertyValueByName(MatEnumNameA);
			}
			return CalibMat;
		}
		set
		{
			if (CalibMat != value)
			{
				CalibMat = value;
				OnPropertyChanged("CalibMatA");
			}
		}
	}

	public virtual string CurValueA
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

	public string DefaultValueA
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
				CurValueA = value;
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

	public object CalibData => Operation.GetPropertyValueByName(Name);

	public string ButttonDescriptionA => Name + "CalibResultParametersA";

	public string CommandNameA => Name + ".ShowCalibInfoCommandA";

	public string EnumName => Name + ".EnumEntrysCalibInspectionIdA";

	public string MatEnumNameA => Name + ".CalibMatA";

	public string CalibInspectionIdNameA => Name + ".CalibInspectionIdA";

	public override bool NeedInit => false;

	public bool IsReadOnlyFlagA
	{
		get
		{
			return IsReadOnlyFlag;
		}
		set
		{
			IsReadOnlyFlag = value;
			OnPropertyChanged("IsReadOnlyFlagA");
		}
	}

	public bool IsEditableA
	{
		get
		{
			return IsEditable;
		}
		set
		{
			IsEditable = value;
			OnPropertyChanged("IsEditableA");
		}
	}

	public void SelectedChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			Operation.SetPropertyValueByName(CalibInspectionIdNameA, Value);
			OldValue = Value;
		}
	}

	public override void GetParamValue()
	{
		MyItemState = ItemState.Normal;
		try
		{
			if (!string.IsNullOrEmpty(EnumName))
			{
				EnumEntrysA = (ObservableCollection<object>)Operation.GetPropertyValueByName(EnumName);
			}
			CurValueA = (string)Operation.GetPropertyValueByName(CalibInspectionIdNameA);
			if (!string.IsNullOrEmpty(MatEnumNameA))
			{
				CalibMatA = Operation.GetPropertyValueByName(MatEnumNameA);
			}
			OnPropertyChanged("CalibData");
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
