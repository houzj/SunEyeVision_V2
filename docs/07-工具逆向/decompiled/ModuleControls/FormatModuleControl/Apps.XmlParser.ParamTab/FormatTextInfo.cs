using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Apps.UIHelper.Extern;
using Apps.XmlParser.Variable;
using FormatModuleControl;
using Hik.Util;
using Mvvm.Commands;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class FormatTextInfo : NameValueItem
{
	private TextType txtInfoType = TextType.Subscription;

	private bool canDelete = false;

	private bool isFocus = false;

	public bool IsAdd = false;

	private string tempValue = string.Empty;

	private IList EnumEntrys;

	private Setter IndexOpenSetter = new Setter
	{
		TargetName = "EnumEntrys",
		OperationName = "GetFrontFilterTreeOperation",
		OperationParams = "int"
	};

	private Setter AllOpenSetter = new Setter
	{
		TargetName = "EnumEntrys",
		OperationName = "GetFrontFilterTreeOperation",
		OperationParams = "int;float;string"
	};

	private bool readOnly = false;

	private bool _IsTextDropDownOpen;

	private bool _IsTextPopupVisible = true;

	private DelegateCommand<ObjectEventArgs> _DelParamCommand = null;

	private DelegateCommand<MouseEventArgs> _MouseEnterCommand = null;

	private DelegateCommand<MouseEventArgs> _MouseLeaveCommand = null;

	private DelegateCommand<MouseEventArgs> _DoubleClickCommand = null;

	private ArrayData arrayData;

	private DelegateCommand<TextBoxFocusEventArgs> _FocusedCommand = null;

	private int curSelectionStart = 0;

	private DelegateCommand<RoutedEventArgs> _InitializedCommand = null;

	private DelegateCommand<ObjectEventArgs> _IndexClickCommand = null;

	public TextType TextInfoType
	{
		get
		{
			return txtInfoType;
		}
		set
		{
			if (value != txtInfoType)
			{
				txtInfoType = value;
				OnPropertyChanged("TextInfoType");
			}
		}
	}

	public bool CanDelete
	{
		get
		{
			return canDelete;
		}
		set
		{
			if (canDelete != value)
			{
				canDelete = value;
				OnPropertyChanged("CanDelete");
			}
		}
	}

	public bool IsFocus
	{
		get
		{
			return isFocus;
		}
		set
		{
			if (isFocus != value)
			{
				isFocus = value;
				OnPropertyChanged("IsFocus");
			}
		}
	}

	public string TempValue
	{
		get
		{
			return tempValue;
		}
		set
		{
			if (!(tempValue != value))
			{
				return;
			}
			if (TextInfoType == TextType.Subscription)
			{
				if (!CheckSubscriptionFormat(value))
				{
					return;
				}
			}
			else if (TextType.Pair != TextInfoType)
			{
			}
			tempValue = value;
			OnPropertyChanged("TempValue");
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

	public bool ReadOnly
	{
		get
		{
			return readOnly;
		}
		set
		{
			if (readOnly != value)
			{
				readOnly = value;
				OnPropertyChanged("ReadOnly");
			}
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

	public bool IsTextPopupVisible
	{
		get
		{
			return _IsTextPopupVisible;
		}
		set
		{
			_IsTextPopupVisible = value;
			OnPropertyChanged("IsTextPopupVisible");
		}
	}

	public ICommand DelParamCommand
	{
		get
		{
			if (_DelParamCommand == null)
			{
				_DelParamCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)DelParamCommandExcute);
			}
			return (ICommand)_DelParamCommand;
		}
	}

	public ICommand MouseEnterCommand
	{
		get
		{
			if (_MouseEnterCommand == null)
			{
				_MouseEnterCommand = new DelegateCommand<MouseEventArgs>((Action<MouseEventArgs>)MouseEnterCommandExecute);
			}
			return (ICommand)_MouseEnterCommand;
		}
	}

	public ICommand MouseLeaveCommand
	{
		get
		{
			if (_MouseLeaveCommand == null)
			{
				_MouseLeaveCommand = new DelegateCommand<MouseEventArgs>((Action<MouseEventArgs>)MouseLeaveCommandExecute);
			}
			return (ICommand)_MouseLeaveCommand;
		}
	}

	public ICommand DoubleClickCommand
	{
		get
		{
			if (_DoubleClickCommand == null)
			{
				_DoubleClickCommand = new DelegateCommand<MouseEventArgs>((Action<MouseEventArgs>)DoubleClickCommandExcute);
			}
			return (ICommand)_DoubleClickCommand;
		}
	}

	public ArrayData ArrayData
	{
		get
		{
			return arrayData;
		}
		set
		{
			if (arrayData != value)
			{
				arrayData = value;
				OnPropertyChanged("ArrayData");
			}
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

	public ICommand InitializedCommand
	{
		get
		{
			if (_InitializedCommand == null)
			{
				_InitializedCommand = new DelegateCommand<RoutedEventArgs>((Action<RoutedEventArgs>)InitializedExecute);
			}
			return (ICommand)_InitializedCommand;
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

	public FormatTextInfo()
	{
		IndexOpenSetter.SetParentNode(this);
		AllOpenSetter.SetParentNode(this);
	}

	private bool CheckSubscriptionFormat(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		if (IsContainsOnce(value, "(") && IsContainsOnce(value, ")") && IsContainsOnce(value, "[") && IsContainsOnce(value, "]"))
		{
			return true;
		}
		return false;
	}

	private bool CheckPairFormat(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		if (IsContainsOnce(value, "{") && IsContainsOnce(value, "}") && IsContainsOnce(value, "[") && IsContainsOnce(value, "]"))
		{
			int num = (from a in value.Substring(0, value.IndexOf("["))
				where a == ','
				select a).Count();
			int num2 = value.Where((char a) => a == '(').Count();
			int num3 = value.Where((char a) => a == ')').Count();
			if (num == num2 && num == num3)
			{
				return true;
			}
			return false;
		}
		return false;
	}

	private bool IsContainsOnce(string tempStr, string targetStr)
	{
		int num = tempStr.IndexOf(targetStr);
		int num2 = tempStr.LastIndexOf(targetStr);
		return num != -1 && num == num2;
	}

	private void DelParamCommandExcute(ObjectEventArgs e)
	{
		FormatRow formatRow = GetParentNode<FormatRow>();
		if (formatRow != null)
		{
			formatRow.FormatInfoCollection.Remove(this);
			Format format = formatRow.GetParentNode<Format>();
			if (format != null && format.CurrentInfo == this)
			{
				format.CurrentInfo = null;
			}
		}
	}

	private void MouseEnterCommandExecute(MouseEventArgs e)
	{
		CanDelete = true;
	}

	private void MouseLeaveCommandExecute(MouseEventArgs e)
	{
		CanDelete = false;
	}

	private void DoubleClickCommandExcute(MouseEventArgs e)
	{
		if (ArrayData == null)
		{
			return;
		}
		InsertArrayWindow insertArrayWindow = new InsertArrayWindow(ArrayData);
		Format format = GetParentNode<Format>();
		if (format == null)
		{
			return;
		}
		insertArrayWindow.ArrayData.SetParentNode(format);
		insertArrayWindow.ShowDialog();
		if (!insertArrayWindow.IsFinish)
		{
			return;
		}
		string text = string.Empty;
		for (int i = 0; i < insertArrayWindow.ArrayData.ArrayList.Count; i++)
		{
			text = text + "<" + (insertArrayWindow.ArrayData.ArrayList[i] as ArrayInfo).IndexInfo.CurValueA + ">";
			if (i != insertArrayWindow.ArrayData.ArrayList.Count - 1)
			{
				text += insertArrayWindow.ArrayData.Delimiter.CurValueA;
			}
		}
		TempValue = "{" + text + "}[" + insertArrayWindow.ArrayData.IndexInfo.CurValueA + "]";
		ArrayData = insertArrayWindow.ArrayData;
	}

	private void FocusedCommandExecute(TextBoxFocusEventArgs e)
	{
		curSelectionStart = e.SelectionStart;
		IsTextPopupVisible = true;
		if (!e.IsFocus)
		{
			IsTextDropDownOpen = false;
			return;
		}
		Format format = GetParentNode<Format>();
		if (format == null)
		{
			return;
		}
		format.CurrentRow = null;
		format.CurrentInfo = this;
		switch (TextInfoType)
		{
		case TextType.Symbol:
		case TextType.Pair:
			ReadOnly = true;
			break;
		case TextType.Text:
			ReadOnly = false;
			break;
		case TextType.Subscription:
		{
			if (e.Text.Length <= 1)
			{
				break;
			}
			bool flag = false;
			for (int num = e.SelectionStart - 1; num >= 0; num--)
			{
				if (e.Text[num].Equals('['))
				{
					flag = true;
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
				ReadOnly = false;
			}
			else
			{
				bool flag2 = false;
				for (int num2 = e.SelectionStart - 1; num2 >= 0; num2--)
				{
					if (e.Text[num2].Equals('('))
					{
						flag2 = true;
						break;
					}
				}
				if (flag2)
				{
					flag2 = false;
					for (int j = e.SelectionStart; j < e.Text.Length; j++)
					{
						if (e.Text[j].Equals(')'))
						{
							flag2 = true;
							break;
						}
					}
				}
				ReadOnly = !flag2;
			}
			IsTextDropDownOpen = flag;
			if (GetTopBaseNode() is AlgorithmTabRoot { IsSubscribeInWindow: not false } algorithmTabRoot)
			{
				algorithmTabRoot.SelectedSubscribeParam = this;
				IsTextPopupVisible = false;
				algorithmTabRoot.IsSubscribeIndex = true;
			}
			break;
		}
		}
	}

	private void InitializedExecute(RoutedEventArgs e)
	{
		if (e.Source is FormatTextBox formatTextBox)
		{
			formatTextBox.Tag = formatTextBox;
		}
	}

	private string SetInsertFormat(string insertValue, string valueType, bool isArray)
	{
		string result = "";
		if (string.IsNullOrEmpty(insertValue) || string.IsNullOrEmpty(valueType))
		{
			return result;
		}
		string arg = "%s";
		switch (valueType)
		{
		case "string":
			arg = "%s";
			break;
		case "float":
			arg = "%1.3f";
			break;
		case "int":
			arg = "%0d";
			break;
		}
		return string.Format("{0}{2}({1})", insertValue, arg, isArray ? "[* {,}]" : string.Empty);
	}

	private void IndexClickCommandExecute(ObjectEventArgs e)
	{
		if (curSelectionStart < 1 || curSelectionStart > TempValue.Length - 1)
		{
			return;
		}
		string text = "";
		string text2 = "";
		if (e.Param != null && e.Param is VmIO)
		{
			text = (e.Param as VmIO).ModulePathName;
			text2 = (e.Param as VmIO).TypeName;
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() != "null")
		{
			text = (e.Param as Holder).DisplayNameA;
			if (text.Equals("Lang_VariableManager"))
			{
				VmIO subFilteFromGlobalVariable = GetSubFilteFromGlobalVariable(e.Param);
				if (subFilteFromGlobalVariable != null)
				{
					text = subFilteFromGlobalVariable.ModulePathName;
					text2 = subFilteFromGlobalVariable.TypeName;
				}
			}
		}
		if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
		{
			if (TextInfoType == TextType.Subscription)
			{
				int num = curSelectionStart - TempValue.Substring(0, curSelectionStart).Reverse().ToList()
					.IndexOf('[');
				int num2 = curSelectionStart + TempValue.Substring(curSelectionStart).IndexOf(']');
				TempValue = TempValue.Remove(num, num2 - num);
				string value = "<" + text + ">";
				TempValue = TempValue.Insert(num, value);
				IsTextDropDownOpen = false;
			}
			else if (TextInfoType == TextType.Pair)
			{
				int num3 = TempValue.IndexOf("[");
				int num4 = TempValue.IndexOf("]");
				if ((TempValue[curSelectionStart - 1] == '{' && TempValue[curSelectionStart] == '<') || (TempValue[curSelectionStart - 1] == ',' && TempValue[curSelectionStart] == '}') || (TempValue[curSelectionStart - 1] == ',' && TempValue[curSelectionStart] == '<'))
				{
					string text3 = SetInsertFormat(text, text2, isArray: false);
					TempValue = TempValue.Insert(curSelectionStart, "<" + text3 + ">,");
					IsTextDropDownOpen = false;
				}
				else if (curSelectionStart > num3 && curSelectionStart <= num4)
				{
					TempValue = TempValue.Remove(num3 + 1, num4 - num3 - 1);
					string text4 = (e.Param as BaseParamNode).GetPropertyValueByName("DisplayFullNameA").ToString();
					TempValue = TempValue.Insert(num3 + 1, "<" + text4 + ">");
					IsTextDropDownOpen = false;
				}
				else
				{
					int num5 = curSelectionStart - TempValue.Substring(0, curSelectionStart).Reverse().ToList()
						.IndexOf('{');
					int num6 = curSelectionStart + TempValue.Substring(curSelectionStart).IndexOf('}');
					TempValue = TempValue.Remove(num5, num6 - num5);
					string text5 = SetInsertFormat(text, text2, isArray: false);
					TempValue = TempValue.Insert(num5, "<" + text5 + ">,");
					IsTextDropDownOpen = false;
				}
			}
		}
		AlgorithmTabRoot algorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
		Format selectedSubscribeParam = GetParentNode<Format>();
		if (algorithmTabRoot != null)
		{
			algorithmTabRoot.IsSubscribeIndex = false;
			if (algorithmTabRoot.IsSubscribeInWindow)
			{
				algorithmTabRoot.SelectedSubscribeParam = selectedSubscribeParam;
			}
		}
	}

	private VmIO GetSubFilteFromGlobalVariable(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if ((obj as Holder).HolderTarget != null)
		{
			object obj2 = ActivatorWrap.CreateInstance("Apps.XmlUI.ParamTab.GlobalVarDlgHelper");
			string[] array = new string[0];
			if ((obj as Holder).HolderTarget is List<object> list)
			{
				if (list.Count < 2)
				{
					return null;
				}
				array = list[0] as string[];
			}
			else if ((obj as Holder).HolderTarget is string[] array2)
			{
				array = array2;
			}
			string[] array3 = null;
			array3 = ((array.Length == 0) ? new string[3] { "int", "float", "string" } : array);
			VmModule vmModule = GetVmModule();
			IVmModule localVarModule = vmModule.GetLocalVarModule();
			object returnObj = obj2?.GetType().InvokeMember("ShowVarSelectDlg", BindingFlags.InvokeMethod, null, obj2, new object[3] { true, localVarModule, array3 });
			if (returnObj != null)
			{
				object owner = (returnObj as VarItem).Owner;
				if (owner != null)
				{
					return (owner as VmModule).Outputs.FirstOrDefault((VmIO x) => x.Name == (returnObj as VarItem).Name);
				}
			}
		}
		return null;
	}
}
