using System;
using System.Linq;
using System.Windows.Input;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;

namespace Apps.XmlParser.ParamTab;

public class QueueItemBase : ButtonSelecter
{
	public bool IsAdded = false;

	public bool CanDelete = true;

	public int DisplayIndex;

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

	public int DisplayIndexA
	{
		get
		{
			return DisplayIndex;
		}
		set
		{
			DisplayIndex = value;
			OnPropertyChanged("DisplayIndexA");
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				vmModule.UserData[NameA + ":DisplayIndexA"] = StringUtil.UTF8GetBytesPadZero(DisplayIndex.ToString());
			}
			else
			{
				LogHelper.Error($"模块获取为空,DisplayIndex={DisplayIndex}");
			}
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
		if (!(GetParentNode() is Category category))
		{
			return;
		}
		try
		{
			QueueItemBase[] array = (from QueueItemBase p in category.ItemsA.Where((object p) => p is QueueItemBase)
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

	public void ModifyDisplayIndex()
	{
		if (GetParentNode() is Category { ItemsA: var itemsA })
		{
			QueueItemBase[] array = (from QueueItemBase p in itemsA.Where((object p) => p is QueueItemBase)
				where p.CustomVisibleA
				select p).ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				array[num].DisplayIndexA = num + 1;
			}
		}
	}

	public virtual void DeleteCommandCmdExecute()
	{
		CustomVisibleA = false;
		IsAddedA = false;
		CurValueA = null;
	}
}
