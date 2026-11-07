using System;
using System.Windows;
using System.Windows.Input;
using Mvvm.Commands;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class Boolean : ValueBase, IGroupRunParam
{
	public bool CurValue;

	public bool DefaultValue;

	private DelegateCommand _ValueChangedCommand = null;

	public Thickness Margin = new Thickness(0.0);

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
				if (isInniter)
				{
					NotifyChanges();
				}
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public bool DefaultValueA
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
			CurValueA = !string.IsNullOrEmpty(value) && bool.Parse(value);
		}
	}

	public new ICommand ValueChangedCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	public Thickness MarginA
	{
		get
		{
			return Margin;
		}
		set
		{
			Margin = value;
			OnPropertyChanged("MarginA");
		}
	}

	string IGroupRunParam.DisplayForGroupType => null;

	protected override void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value && SetParamUnit() == 0)
		{
			OldValue = Value;
			if (!IsControlByParent())
			{
				PreExecuteRun();
			}
		}
	}

	protected virtual bool IsControlByParent()
	{
		return false;
	}

	public void SetParam()
	{
		if (OldValue != Value && SetParamUnit() == 0)
		{
			OldValue = Value;
		}
	}

	public override int GetParamUnit()
	{
		int paramUnit = base.GetParamUnit();
		if (paramUnit == 0)
		{
			OldValue = Value;
		}
		return paramUnit;
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}
}
