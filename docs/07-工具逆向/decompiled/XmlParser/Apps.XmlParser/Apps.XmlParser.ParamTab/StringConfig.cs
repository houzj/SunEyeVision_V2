using System;
using System.Collections;
using System.Linq;
using System.Windows.Input;
using Apps.Log;
using Apps.UIHelper.Extern;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class StringConfig : BridgeItemBase
{
	public int Length;

	private IList EnumEntrys;

	private Setter IndexOpenSetter = new Setter
	{
		TargetName = "EnumEntrys",
		OperationName = "GetFrontAndSelfFilterWithoutTSTreeOperation",
		OperationParams = "int;float;string"
	};

	private DelegateCommand<ObjectEventArgs> _IndexClickCommand = null;

	private DelegateCommand<TextBoxFocusEventArgs> _FocusedCommand = null;

	private int curSelectionStart = 0;

	private bool _IsTextDropDownOpen;

	public int LengthA
	{
		get
		{
			return Length;
		}
		set
		{
			Length = value;
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
			}
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

	private void IndexClickCommandExecute(ObjectEventArgs e)
	{
		VmIO vmIO;
		if ((vmIO = e.Param as VmIO) == null)
		{
			return;
		}
		Action<object> action = (object o) =>
		{
			try
			{
				string text = o.ToString();
				if (curSelectionStart >= 0 && curSelectionStart < text.Length)
				{
					int num = curSelectionStart - text.Substring(0, curSelectionStart).Reverse().ToList()
						.IndexOf('{');
					int num2 = curSelectionStart + text.Substring(curSelectionStart).IndexOf('}');
					text = text.Remove(num, num2 - num);
					curSelectionStart = num;
					string modulePathName = vmIO.ModulePathName;
					ItemsHelper.SetBindingProperty(this, BindingPathA, text.Insert(curSelectionStart, modulePathName));
				}
				else
				{
					LogHelper.Error($"StringConfig IndexClickCommandExecute failed:curSelectionStart[{curSelectionStart}] out of range[{text.Length}]");
				}
				IsTextDropDownOpen = false;
			}
			catch (Exception ex)
			{
				LogHelper.Error("StringConfig IndexClickCommandExecute failed:" + ex.Message + ex.StackTrace);
			}
		};
		ItemsHelper.GetBindingProperty(this, BindingPathA, action);
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
				if (e.Text[num].Equals('{'))
				{
					flag = true;
					break;
				}
				if (e.Text[num].Equals('}') || num == 0)
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
					if (e.Text[i].Equals('}'))
					{
						flag = true;
						break;
					}
					if (e.Text[i].Equals('{') || i == e.Text.Length - 1)
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
}
