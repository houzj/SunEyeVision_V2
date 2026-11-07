using System;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class SubModuleInfoItem : NameValueItem
{
	private string _DisplayType;

	private bool _Checked_Btn1;

	private bool _Checked_Btn2;

	private bool _Checked_Btn3;

	private int _TabNum;

	private DelegateCommand<object> _ClickCommand_1 = null;

	private DelegateCommand<object> _ClickCommand_2 = null;

	private DelegateCommand<object> _ClickCommand_3 = null;

	public SubModuleInfo info
	{
		get
		{
			if (!(GetParentNode() is SubData_EdgeInspGroup { info: var result }))
			{
				return null;
			}
			return result;
		}
	}

	public override string DisplayType
	{
		get
		{
			return _DisplayType;
		}
		set
		{
			if (_DisplayType != value)
			{
				_DisplayType = value;
			}
		}
	}

	public override string DisplayNameA
	{
		get
		{
			if (info != null)
			{
				return info.TypeNameA;
			}
			return null;
		}
		set
		{
			OnPropertyChanged("DisplayNameA");
		}
	}

	public string DisplayNameA_Btn1
	{
		get
		{
			if (info == null || info.AlgorithmRoot == null || info.AlgorithmRoot.Tabs.Count <= 0 || !(info.AlgorithmRoot.Tabs[0] is Tab { NameA: var nameA }))
			{
				return null;
			}
			return nameA;
		}
	}

	public string DisplayNameA_Btn2
	{
		get
		{
			if (info == null || info.AlgorithmRoot == null || info.AlgorithmRoot.Tabs.Count <= 1 || !(info.AlgorithmRoot.Tabs[1] is Tab { NameA: var nameA }))
			{
				return null;
			}
			return nameA;
		}
	}

	public string DisplayNameA_Btn3
	{
		get
		{
			if (info == null || info.AlgorithmRoot == null || info.AlgorithmRoot.Tabs.Count <= 2 || !(info.AlgorithmRoot.Tabs[2] is Tab { NameA: var nameA }))
			{
				return null;
			}
			return nameA;
		}
	}

	public bool Checked_Btn1
	{
		get
		{
			return _Checked_Btn1;
		}
		set
		{
			if (_Checked_Btn1 != value)
			{
				_Checked_Btn1 = value;
				OnPropertyChanged("Checked_Btn1");
			}
		}
	}

	public bool Checked_Btn2
	{
		get
		{
			return _Checked_Btn2;
		}
		set
		{
			if (_Checked_Btn2 != value)
			{
				_Checked_Btn2 = value;
				OnPropertyChanged("Checked_Btn2");
			}
		}
	}

	public bool Checked_Btn3
	{
		get
		{
			return _Checked_Btn3;
		}
		set
		{
			if (_Checked_Btn3 != value)
			{
				_Checked_Btn3 = value;
				OnPropertyChanged("Checked_Btn3");
			}
		}
	}

	public int TabNum
	{
		get
		{
			return _TabNum;
		}
		set
		{
			if (_TabNum != value)
			{
				_TabNum = value;
				OnPropertyChanged("TabNum");
			}
		}
	}

	public ICommand ClickCommand_1
	{
		get
		{
			if (_ClickCommand_1 == null)
			{
				_ClickCommand_1 = new DelegateCommand<object>((Action<object>)ClickCommandCmdExecute_1);
			}
			return (ICommand)_ClickCommand_1;
		}
	}

	public ICommand ClickCommand_2
	{
		get
		{
			if (_ClickCommand_2 == null)
			{
				_ClickCommand_2 = new DelegateCommand<object>((Action<object>)ClickCommandCmdExecute_2);
			}
			return (ICommand)_ClickCommand_2;
		}
	}

	public ICommand ClickCommand_3
	{
		get
		{
			if (_ClickCommand_3 == null)
			{
				_ClickCommand_3 = new DelegateCommand<object>((Action<object>)ClickCommandCmdExecute_3);
			}
			return (ICommand)_ClickCommand_3;
		}
	}

	protected virtual void DoClick(object obj, SubModuleInfo source, int index)
	{
		if (!(obj is bool) || !(GetParentNode() is SubData_EdgeInspGroup subData_EdgeInspGroup))
		{
			return;
		}
		subData_EdgeInspGroup.IsOpenDetail = (bool)obj;
		if (subData_EdgeInspGroup.IsOpenDetail)
		{
			AddList_EdgeInspGroup addList_EdgeInspGroup = GetParentNode<AddList_EdgeInspGroup>();
			try
			{
				foreach (SubData_EdgeInspGroup item in addList_EdgeInspGroup.EnumEntrysA)
				{
					if (item != subData_EdgeInspGroup && item.IsOpenDetail)
					{
						item.IsOpenDetail = false;
						(item.EnumEntrysA[1] as SubModuleInfoItem).Checked_Btn1 = false;
						(item.EnumEntrysA[1] as SubModuleInfoItem).Checked_Btn2 = false;
						if ((item.EnumEntrysA[1] as SubModuleInfoItem).TabNum == 3)
						{
							(item.EnumEntrysA[1] as SubModuleInfoItem).Checked_Btn3 = false;
						}
						item.SetListSource(null, index);
					}
				}
			}
			catch
			{
			}
			subData_EdgeInspGroup.SetListSource(source, index);
		}
		else
		{
			subData_EdgeInspGroup.SetListSource(null, index);
		}
	}

	private void ClickCommandCmdExecute_1(object obj)
	{
		DoClick(obj, info, 0);
	}

	private void ClickCommandCmdExecute_2(object obj)
	{
		DoClick(obj, info, 1);
	}

	private void ClickCommandCmdExecute_3(object obj)
	{
		DoClick(obj, info, 2);
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
