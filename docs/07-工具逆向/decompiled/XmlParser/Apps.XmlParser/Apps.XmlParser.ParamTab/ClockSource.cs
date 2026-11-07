using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class ClockSource : NameValueItem
{
	private bool CurValue;

	public int Milliseconds;

	private ICommand tickCommand = null;

	public bool CurValueA
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

	public int MillisecondsA
	{
		get
		{
			return Milliseconds;
		}
		set
		{
			if (Milliseconds != value)
			{
				Milliseconds = value;
				OnPropertyChanged("MillisecondsA");
			}
		}
	}

	public ICommand TickCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = tickCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)TickCommandExecute);
				ICommand command2 = (ICommand)val;
				tickCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public override bool CustomVisibleA
	{
		get
		{
			return true;
		}
		set
		{
		}
	}

	private void TickCommandExecute()
	{
		CurValueA = !CurValue;
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
