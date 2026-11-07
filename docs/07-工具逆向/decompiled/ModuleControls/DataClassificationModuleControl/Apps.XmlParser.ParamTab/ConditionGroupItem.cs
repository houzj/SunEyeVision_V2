using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ConditionGroupItem : NameValueItem
{
	public int _ConditionGroupID;

	public string _ConditionGroupName;

	public ObservableCollection<ConditionItem> _ConditionItems;

	private ICommand _AddConditionItemCommand = null;

	private ICommand _DeleteConditionGroupCommand = null;

	public DataClassificationConfig config { get; set; }

	public ClassificationItem owner { get; set; }

	public int ConditionGroupID
	{
		get
		{
			return _ConditionGroupID;
		}
		set
		{
			if (_ConditionGroupID != value)
			{
				_ConditionGroupID = value;
				OnPropertyChanged("ConditionGroupID");
			}
		}
	}

	public string ConditionGroupName
	{
		get
		{
			return _ConditionGroupName;
		}
		set
		{
			if (_ConditionGroupName != value)
			{
				_ConditionGroupName = value;
				OnPropertyChanged("ConditionGroupName");
			}
		}
	}

	public int SelectTypeIndex { get; set; }

	public VmModule vmModule { get; set; }

	public ObservableCollection<ConditionItem> ConditionItems
	{
		get
		{
			if (_ConditionItems == null)
			{
				_ConditionItems = new ObservableCollection<ConditionItem>();
			}
			return _ConditionItems;
		}
		set
		{
			if (_ConditionItems != value)
			{
				_ConditionItems = value;
				OnPropertyChanged("ConditionItems");
			}
		}
	}

	public ICommand AddConditionItemCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_AddConditionItemCommand == null)
			{
				_AddConditionItemCommand = (ICommand)new DelegateCommand((Action)AddConditionItemCmdExecute);
			}
			return _AddConditionItemCommand;
		}
	}

	public ICommand DeleteConditionGroupCommand
	{
		get
		{
			if (_DeleteConditionGroupCommand == null)
			{
				_DeleteConditionGroupCommand = (ICommand)new DelegateCommand<object>((Action<object>)DeleteConditionGroupCmdExecute);
			}
			return _DeleteConditionGroupCommand;
		}
	}

	public void AddConditionItemCmdExecute()
	{
		if (Enumerable.Count(ConditionItems) >= 10)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_ClassConditionCountError") + $"【{10}】";
			HikMessageBox.Show(strMsg);
		}
		else
		{
			if (owner == null || config == null)
			{
				return;
			}
			ConditionItem conditionItem = new ConditionItem();
			conditionItem.vmModule = vmModule;
			conditionItem.ConditionGroupName = ConditionGroupName;
			conditionItem.ConditionGroupID = ConditionGroupID;
			int i;
			for (i = 0; !CheckConditionName(i); i++)
			{
				if (i >= 10)
				{
					return;
				}
			}
			conditionItem.ConditionID = i;
			conditionItem.owner = this;
			conditionItem.config = config;
			conditionItem.classItem = owner;
			conditionItem.TypeName = ((SelectTypeIndex == 0) ? "int" : "float");
			conditionItem.SetParentNode(this);
			ConditionItems.Add(conditionItem);
			config.SetConditionItem(owner);
		}
	}

	public void DeleteConditionGroupCmdExecute(object data)
	{
		if (owner == null || config == null)
		{
			return;
		}
		foreach (ConditionItem conditionItem in ConditionItems)
		{
			conditionItem.DeleteBettweenSubData();
		}
		ConditionItems.Clear();
		owner.ConditionGroupItems.Remove(this);
		config.SetConditionGroup(owner);
	}

	public bool CheckConditionName(int seq)
	{
		foreach (ConditionItem conditionItem in ConditionItems)
		{
			if (conditionItem.ConditionID == seq)
			{
				return false;
			}
		}
		return true;
	}
}
