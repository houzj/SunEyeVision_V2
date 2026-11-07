using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Interface;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class SubData : NameValueItem
{
	public ObservableCollection<object> EnumEntrys;

	private DelegateCommand _DeleteCommand = null;

	public ObservableCollection<object> EnumEntrysA
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
			}
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	protected virtual void DeleteCommandCmdExecute()
	{
		if (!(parentNode is AddList { EnumEntrysA: not null } addList) || !addList.EnumEntrysA.Contains(this))
		{
			return;
		}
		if (EnumEntrys != null)
		{
			foreach (object enumEntry in EnumEntrys)
			{
				if (enumEntry is IDelete)
				{
					(enumEntry as IDelete).Delete();
				}
			}
		}
		addList.EnumEntrysA.Remove(this);
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
