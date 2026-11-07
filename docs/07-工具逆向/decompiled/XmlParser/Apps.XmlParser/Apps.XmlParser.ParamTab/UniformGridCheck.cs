using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class UniformGridCheck : UniformGridBase
{
	private DelegateCommand _ValueChangedCommand = null;

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

	private void ValueChangedCmdExecute()
	{
		isControlChildRun = true;
		foreach (object item in EnumEntrysA)
		{
			if (item is BooleanCheck booleanCheck)
			{
				booleanCheck.CurValueA = CurValueA;
			}
		}
		isControlChildRun = false;
		if (OldValue != Value && SetParamUnit() == 0)
		{
			OldValue = Value;
			PreExecuteRun();
		}
	}
}
