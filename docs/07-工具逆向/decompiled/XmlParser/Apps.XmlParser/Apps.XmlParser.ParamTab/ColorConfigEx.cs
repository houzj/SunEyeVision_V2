using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class ColorConfigEx : BridgeItemBase
{
	private DelegateCommand<object> _ValueChangedCommand = null;

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand<object>((Action<object>)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	private void ValueChangedCmdExecute(object obj)
	{
		if (IsUnConnect() || !(obj is ColorConfigEx { BridgeObjA: Object bridgeObjA }))
		{
			return;
		}
		string text = "#";
		text += bridgeObjA.okcolorA.Substring(3);
		if (OldValue != text)
		{
			Value = text;
			int num = SetParamUnit();
			OldValue = text;
			if (num >= 0)
			{
				PreExecuteRun();
			}
		}
	}

	public override int GetParamUnit()
	{
		return GetParamUnitByZmq();
	}

	public override int SetParamUnit()
	{
		string value = Value;
		return SetParamUnitByZmq(value);
	}
}
