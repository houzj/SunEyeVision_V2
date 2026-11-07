using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ClassificationItem : NameValueItem
{
	public string _ClassificationName;

	public ConditionGroupItem _SelectedConditionGroupItem;

	public ObservableCollection<ConditionGroupItem> _ConditionGroupItems;

	private ICommand _AddConditionGroupItemCommand = null;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	private ICommand _DeleteClassificationCommand = null;

	private ICommand _SortClassificationCommand = null;

	public DataClassificationConfig config { get; set; }

	public VmModule vmModule { get; set; }

	public int Seq
	{
		get
		{
			if (config != null)
			{
				int num = 0;
				foreach (ClassificationItem classificationItem in config.ClassificationItems)
				{
					if (classificationItem == this)
					{
						return num;
					}
					num++;
				}
			}
			return 0;
		}
	}

	public bool isCanUp
	{
		get
		{
			if (config == null)
			{
				return false;
			}
			if (Seq == 0)
			{
				return false;
			}
			return true;
		}
	}

	public bool isCanDown
	{
		get
		{
			if (config == null)
			{
				return false;
			}
			if (Seq == config.ClassificationItems.Count - 1)
			{
				return false;
			}
			return true;
		}
	}

	public string ClassificationName
	{
		get
		{
			return _ClassificationName;
		}
		set
		{
			if (_ClassificationName != value)
			{
				_ClassificationName = value;
				OnPropertyChanged("ClassificationName");
			}
		}
	}

	public ConditionGroupItem SelectedConditionGroupItem
	{
		get
		{
			return _SelectedConditionGroupItem;
		}
		set
		{
			if (_SelectedConditionGroupItem != value)
			{
				_SelectedConditionGroupItem = value;
				OnPropertyChanged("SelectedConditionGroupItem");
			}
		}
	}

	public ObservableCollection<ConditionGroupItem> ConditionGroupItems
	{
		get
		{
			if (_ConditionGroupItems == null)
			{
				_ConditionGroupItems = new ObservableCollection<ConditionGroupItem>();
			}
			return _ConditionGroupItems;
		}
		set
		{
			if (_ConditionGroupItems != value)
			{
				_ConditionGroupItems = value;
				OnPropertyChanged("ConditionGroupItems");
			}
		}
	}

	public ICommand AddConditionGroupItemCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_AddConditionGroupItemCommand == null)
			{
				_AddConditionGroupItemCommand = (ICommand)new DelegateCommand((Action)AddConditionGroupItemCmdExecute);
			}
			return _AddConditionGroupItemCommand;
		}
	}

	public ICommand PreviewMouseWheelCommand
	{
		get
		{
			if (_PreviewMouseWheelCommand == null)
			{
				_PreviewMouseWheelCommand = new DelegateCommand<object>((Action<object>)PreviewMouseWheelCommandExcute);
			}
			return (ICommand)_PreviewMouseWheelCommand;
		}
	}

	public ICommand DeleteClassificationCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteClassificationCommand == null)
			{
				_DeleteClassificationCommand = (ICommand)new DelegateCommand((Action)DeleteClassificationCmdExecute);
			}
			return _DeleteClassificationCommand;
		}
	}

	public ICommand SortClassificationCommand
	{
		get
		{
			if (_SortClassificationCommand == null)
			{
				_SortClassificationCommand = (ICommand)new DelegateCommand<object>((Action<object>)SortClassificationCmdExecute);
			}
			return _SortClassificationCommand;
		}
	}

	public void AddConditionGroupItemCmdExecute()
	{
		if (Enumerable.Count(ConditionGroupItems) >= 10)
		{
			string strMsg = AppLocalizationService.GetLang("Lang_ConditionGroupCountLimitError") + $"【{10}】";
			HikMessageBox.Show(strMsg);
			return;
		}
		int i;
		for (i = 0; !CheckConditionName(i); i++)
		{
			if (i >= 10)
			{
				return;
			}
		}
		ConditionGroupItem conditionGroupItem = new ConditionGroupItem();
		conditionGroupItem.ConditionGroupID = i;
		conditionGroupItem.ConditionGroupName = AppLocalizationService.GetLang("RunParam_Conditioned") + i;
		conditionGroupItem.vmModule = vmModule;
		conditionGroupItem.owner = this;
		conditionGroupItem.SetParentNode(this);
		conditionGroupItem.config = config;
		ConditionGroupItems.Add(conditionGroupItem);
		config.SetConditionGroup(this);
		SelectedConditionGroupItem = conditionGroupItem;
	}

	public bool CheckConditionName(int seq)
	{
		foreach (ConditionGroupItem conditionGroupItem in ConditionGroupItems)
		{
			if (conditionGroupItem.ConditionGroupID == seq)
			{
				return false;
			}
		}
		return true;
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
		if (hasRegistered)
		{
		}
		ScrollViewer sv;
		if ((sv = obj as ScrollViewer) != null)
		{
			sv.PreviewMouseWheel += (object sender, MouseWheelEventArgs e) =>
			{
				MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
				{
					RoutedEvent = UIElement.MouseWheelEvent,
					Source = sender
				};
				sv.RaiseEvent(e2);
			};
			hasRegistered = true;
		}
	}

	public void DeleteClassificationCmdExecute()
	{
		if (config != null)
		{
			foreach (ConditionGroupItem conditionGroupItem in ConditionGroupItems)
			{
				foreach (ConditionItem conditionItem in conditionGroupItem.ConditionItems)
				{
					conditionItem.DeleteBettweenSubData();
				}
				conditionGroupItem.ConditionItems.Clear();
			}
			ConditionGroupItems.Clear();
			config.ClassificationItems.Remove(this);
		}
		config.NotifySeq();
		config.UpdateOuputIO();
		config.SetClassification();
	}

	public void SortClassificationCmdExecute(object sortType)
	{
		if (config != null)
		{
			int seq = Seq;
			if ((sortType.ToString() == "up" && seq == 0) || (sortType.ToString() == "down" && seq == config.ClassificationItems.Count - 1))
			{
				return;
			}
			config.ClassificationItems.Remove(this);
			if (sortType.ToString() == "up")
			{
				int index = seq - 1;
				config.ClassificationItems.Insert(index, this);
			}
			else
			{
				int index2 = seq + 1;
				config.ClassificationItems.Insert(index2, this);
			}
			config.NotifySeq();
		}
		config.SetClassificationChange();
	}
}
