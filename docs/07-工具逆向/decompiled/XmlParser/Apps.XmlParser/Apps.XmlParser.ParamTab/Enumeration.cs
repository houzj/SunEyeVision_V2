using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Mvvm.Commands;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class Enumeration : NameValueItem, IGroupRunParam
{
	public delegate void SelectedChangedHandler(object sender, EventArgs e);

	public string strChoice = "Lang_PlzChoose";

	public ObservableCollection<object> EnumEntrys;

	public int CurValue;

	public int DefaultValue;

	public string CurrentName;

	private ICommand _SelectedChangedCommand = null;

	protected bool IsSetValue { get; set; } = false;

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
				IsSetValue = true;
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

	public string CurrentNameA
	{
		get
		{
			return CurrentName;
		}
		set
		{
			if (CurrentName != value)
			{
				CurrentName = value;
				OnPropertyChanged("CurrentNameA");
			}
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)SelectedChangedCmdExecute);
			}
			return _SelectedChangedCommand;
		}
		set
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected Obj, but got Unknown
			_SelectedChangedCommand = (ICommand)(DelegateCommand)value;
		}
	}

	string IGroupRunParam.DisplayForGroupType => null;

	public event SelectedChangedHandler OnSelectedChanged;

	public virtual void SelectedChangedCmdExecute(object data)
	{
		if (data == null)
		{
			return;
		}
		int num = (int)data;
		if (num < 0 || IsUnConnect())
		{
			return;
		}
		if (OldValue != Value)
		{
			string oldValue = OldValue;
			int num2 = SetParamUnit();
			if (num2 >= 0 && (oldValue != null || IsSetValue))
			{
				PreExecuteRun();
			}
			OldValue = Value;
		}
		if (OnSelectedChanged != null)
		{
			EventArgs e = new EventArgs();
			OnSelectedChanged(this, e);
		}
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}
}
