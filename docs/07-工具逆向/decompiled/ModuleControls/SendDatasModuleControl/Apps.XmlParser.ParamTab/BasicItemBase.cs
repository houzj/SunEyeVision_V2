using System;
using System.Linq;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class BasicItemBase : ButtonSelecter
{
	public bool IsAdded = false;

	public bool CanDelete = true;

	private DelegateCommand _DeleteCommand = null;

	public virtual bool IsAddedA
	{
		get
		{
			return IsAdded;
		}
		set
		{
			IsAdded = value;
			OnPropertyChanged("IsAddedA");
		}
	}

	public virtual bool CanDeleteA
	{
		get
		{
			return CanDelete;
		}
		set
		{
			CanDelete = value;
			OnPropertyChanged("CanDeleteA");
		}
	}

	public virtual ICommand DeleteCommand
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

	public void ModifyDeleteBtnVisibility()
	{
		if (!(GetParentNode() is BasicItemsSend { Items: not null } basicItemsSend))
		{
			return;
		}
		try
		{
			BasicItemBase[] array = (from BasicItemBase p in basicItemsSend.Items.Where((BasicItemSend p) => p != null)
				where p.CustomVisibleA
				select p).ToArray();
			if (array.Length > 1)
			{
				for (int num = 0; num < array.Length; num++)
				{
					array[num].CanDeleteA = true;
				}
			}
			else if (array.Length == 1)
			{
				array[0].CanDeleteA = false;
			}
		}
		catch (Exception)
		{
		}
	}

	public virtual void DeleteCommandCmdExecute()
	{
	}

	internal void ModifyDisplayIndex()
	{
		if (!(GetParentNode() is BasicItemsSend { Items: not null, Items: var items }))
		{
			return;
		}
		BasicItemBase[] array = (from BasicItemBase p in items.Where((BasicItemSend p) => p != null)
			where p.CustomVisibleA
			select p).ToArray();
		for (int num = 0; num < array.Length; num++)
		{
			if (array[num] is BasicItemSend)
			{
				(array[num] as BasicItemSend).DisplayIndexA = (num + 1).ToString();
			}
		}
	}
}
