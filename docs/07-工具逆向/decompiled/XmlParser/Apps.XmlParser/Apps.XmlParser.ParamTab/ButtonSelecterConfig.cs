using System;
using System.Collections;
using System.Linq;
using System.Windows.Input;
using Apps.UIHelper.Extern;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecterConfig : BridgeItemBase
{
	public Setter DropOpenSetter;

	private IList EnumEntrys;

	public string ButtonParam;

	private DelegateCommand _DropDownCommand = null;

	private DelegateCommand<ObjectEventArgs> _ClickCommand = null;

	private bool _IsTextDropDownOpen;

	private bool _IsDropDownOpen;

	private Setter _IndexOpenSetter = null;

	private DelegateCommand<TextBoxFocusEventArgs> _FocusedCommand = null;

	private int curSelectionStart = 0;

	private DelegateCommand<ObjectEventArgs> _IndexClickCommand = null;

	public Setter DropOpenSetterA
	{
		get
		{
			return DropOpenSetter;
		}
		set
		{
			DropOpenSetter = value;
			OnPropertyChanged("DropOpenSetterA");
		}
	}

	public IList EnumEntrysA
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
				OnPropertyChanged("EnumEntrysA");
			}
		}
	}

	public ICommand DropDownCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DropDownCommand == null)
			{
				_DropDownCommand = new DelegateCommand((Action)DropDownCommandExecute);
			}
			return (ICommand)_DropDownCommand;
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)ClickCommandExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public bool IsTextDropDownOpen
	{
		get
		{
			return _IsTextDropDownOpen;
		}
		set
		{
			_IsTextDropDownOpen = value;
			OnPropertyChanged("IsTextDropDownOpen");
		}
	}

	public bool ReadOnly => false;

	public bool IsDropDownOpen
	{
		get
		{
			return _IsDropDownOpen;
		}
		set
		{
			_IsDropDownOpen = value;
			OnPropertyChanged("IsDropDownOpen");
		}
	}

	private Setter IndexOpenSetter
	{
		get
		{
			if (_IndexOpenSetter == null)
			{
				_IndexOpenSetter = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontAndSelfFilterTreeOperation",
					OperationParams = "int"
				};
			}
			return _IndexOpenSetter;
		}
	}

	public ICommand FocusedCommand
	{
		get
		{
			if (_FocusedCommand == null)
			{
				_FocusedCommand = new DelegateCommand<TextBoxFocusEventArgs>((Action<TextBoxFocusEventArgs>)FocusedCommandExecute);
			}
			return (ICommand)_FocusedCommand;
		}
	}

	public ICommand IndexClickCommand
	{
		get
		{
			if (_IndexClickCommand == null)
			{
				_IndexClickCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)IndexClickCommandExecute);
			}
			return (ICommand)_IndexClickCommand;
		}
	}

	private void DropDownCommandExecute()
	{
		if (DropOpenSetter != null)
		{
			if (DropOpenSetter.Operation == null)
			{
				DropOpenSetter.CreatOperateInstance();
			}
			if (DropOpenSetter.Operation != null)
			{
				DropOpenSetter.Operate(null);
			}
		}
	}

	private void ClickCommandExecute(ObjectEventArgs e)
	{
		if (e.Param is VmIO { ModulePathName: var modulePathName })
		{
			ItemsHelper.SetBindingProperty(this, BindingPathA, modulePathName);
			IsDropDownOpen = false;
		}
	}

	private void FocusedCommandExecute(TextBoxFocusEventArgs e)
	{
		if (!e.IsFocus)
		{
			IsTextDropDownOpen = false;
		}
		else
		{
			if (ReadOnly || e.Text.Length <= 1)
			{
				return;
			}
			bool flag = false;
			for (int num = e.SelectionStart - 1; num >= 0; num--)
			{
				if (e.Text[num].Equals('['))
				{
					flag = true;
					break;
				}
				if (e.Text[num].Equals(']') || num == 0)
				{
					flag = false;
					break;
				}
			}
			if (flag)
			{
				flag = false;
				for (int i = e.SelectionStart; i < e.Text.Length; i++)
				{
					if (e.Text[i].Equals(']'))
					{
						flag = true;
						break;
					}
					if (e.Text[i].Equals('[') || i == e.Text.Length - 1)
					{
						flag = false;
						break;
					}
				}
			}
			if (flag)
			{
				if (IndexOpenSetter.Operation == null)
				{
					IndexOpenSetter.CreatOperateInstance();
				}
				IndexOpenSetter.SetParentNode(this);
				if (IndexOpenSetter.Operation != null)
				{
					IndexOpenSetter.Operate(null);
				}
				curSelectionStart = e.SelectionStart;
			}
			IsTextDropDownOpen = flag;
		}
	}

	private void IndexClickCommandExecute(ObjectEventArgs e)
	{
		Action<object> action = (object o) =>
		{
			string text = o.ToString();
			int num = curSelectionStart - text.Substring(0, curSelectionStart).Reverse().ToList()
				.IndexOf('[');
			int num2 = curSelectionStart + text.Substring(curSelectionStart).IndexOf(']');
			curSelectionStart = num;
			if (e.Param is VmIO vmIO && text != null)
			{
				string modulePathName = vmIO.ModulePathName;
				text = text.Remove(num, num2 - num);
				ItemsHelper.SetBindingProperty(this, BindingPathA, text.Insert(curSelectionStart, modulePathName));
				IsTextDropDownOpen = false;
			}
			else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() == "null")
			{
				text = text.Remove(num, num2 - num);
				ItemsHelper.SetBindingProperty(this, BindingPathA, text);
				IsTextDropDownOpen = false;
			}
		};
		ItemsHelper.GetBindingProperty(this, BindingPathA, action);
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
