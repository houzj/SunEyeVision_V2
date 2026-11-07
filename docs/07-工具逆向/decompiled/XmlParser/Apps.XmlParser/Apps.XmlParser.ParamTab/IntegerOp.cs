using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class IntegerOp : NameValueItem
{
	public int MinValue;

	public int MaxValue;

	public int IncValue = 1;

	public int CurValue;

	public int DefaultValue;

	public string Unit;

	private DelegateCommand _ValueChangedCommand = null;

	public int CurValueA
	{
		get
		{
			if (string.IsNullOrEmpty(OldValue))
			{
				OldValue = CurValue.ToString();
			}
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				if (value > MaxValue || value < MinValue)
				{
					CurValue = value;
				}
				else
				{
					int num = (value - MinValue) / IncValue;
					CurValue = num * IncValue + MinValue;
				}
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

	public string UnitA
	{
		get
		{
			return Unit;
		}
		set
		{
			Unit = value;
			OnPropertyChanged("UnitA");
		}
	}

	public int MinValueA
	{
		get
		{
			return MinValue;
		}
		set
		{
			MinValue = value;
			OnPropertyChanged("MinValueA");
		}
	}

	public int MaxValueA
	{
		get
		{
			return MaxValue;
		}
		set
		{
			MaxValue = value;
			OnPropertyChanged("MaxValueA");
		}
	}

	public int IncValueA
	{
		get
		{
			return IncValue;
		}
		set
		{
			IncValue = value;
			OnPropertyChanged("IncValueA");
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
			int curValueA = int.Parse(value);
			CurValueA = curValueA;
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	protected void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value && SetParamUnit() >= 0)
		{
			OldValue = Value;
			PreExecuteRun();
		}
	}

	public override void Check(string value)
	{
		RangeChecker rangeChecker = new RangeChecker();
		rangeChecker.Check(new object[2] { MinValue, MaxValue }, value, DisplayNameA);
	}
}
