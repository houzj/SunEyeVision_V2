using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class Command : NameValueItem
{
	public string CurValue;

	public string DefaultValue;

	public Setter CommandSetter;

	public double _MaxWidth = double.MaxValue;

	private DelegateCommand _ClickCommand = null;

	public Setter CommandSetterA
	{
		get
		{
			return CommandSetter;
		}
		set
		{
			CommandSetter = value;
			OnPropertyChanged("CommandSetterA");
		}
	}

	public double MaxWidthA
	{
		get
		{
			return _MaxWidth;
		}
		set
		{
			_MaxWidth = value;
			OnPropertyChanged("MaxWidthA");
		}
	}

	public string CurValueA
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
			return CurValue;
		}
		set
		{
			CurValue = value;
		}
	}

	public override bool NeedInit => false;

	public virtual ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCommandCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	private void ClickCommandCmdExecute()
	{
		if (CommandSetter != null)
		{
			if (CommandSetter.Operation == null)
			{
				CommandSetter.CreatOperateInstance();
			}
			if (CommandSetter.Operation != null)
			{
				CommandSetter.Operate(null);
			}
		}
		SetParamUnit();
		OnPropertyChanged("ClickCommandCmdExecuteA");
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override void XmlLoadedTask()
	{
		base.XmlLoadedTask();
		CommandSetter?.Init();
	}
}
