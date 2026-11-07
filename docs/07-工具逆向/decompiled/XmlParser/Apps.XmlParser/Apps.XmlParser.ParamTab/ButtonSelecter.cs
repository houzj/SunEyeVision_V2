using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Apps.XmlParser.Variable;
using Hik.Util;
using Mvvm.Commands;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter : NameValueItem
{
	public string selectedFilterType;

	public const string NullSymbol = "null";

	public const string AllSymbol = "全部";

	public static string strChoice = AppLocalizationService.GetLang("Lang_PlzChoose");

	public Setter DropOpenSetter;

	public bool ReadOnly = false;

	public string CurValue;

	private IList EnumEntrys;

	private Setter IndexOpenSetter = new Setter
	{
		TargetName = "EnumEntrys",
		OperationName = "GetFrontFilterTreeOperation",
		OperationParams = "int"
	};

	public string ButtonParam;

	private DelegateCommand _DropDownCommand = null;

	private DelegateCommand<ObjectEventArgs> _ClickCommand = null;

	private DelegateCommand<ObjectEventArgs> _IndexClickCommand = null;

	private DelegateCommand<TextBoxFocusEventArgs> _FocusedCommand = null;

	private int curSelectionStart = 0;

	private bool _IsTextDropDownOpen;

	private bool _IsDropDownOpen;

	public bool SubscribeInPopWindowEnable = true;

	private bool _IsTextPopupVisible = true;

	private DelegateCommand popSubscribeWindowCommand = null;

	private AlgorithmTabRoot algorithmTabRoot = null;

	public virtual string SelectedFilterType => selectedFilterType;

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

	public override bool NeedInit => false;

	public override string Value => CurValueA;

	public int Type { get; set; } = 0;

	public string OldCurValue { get; set; }

	public virtual string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (!(CurValue != value))
			{
				return;
			}
			OldCurValue = CurValue;
			CurValue = value;
			if (OldCurValue != null && CurValue != null)
			{
				NameValueItem.logManager?.LogCreate("信息", FullDisplayName + " " + AppLocalizationService.GetLang("参数修改") + OldCurValue + AppLocalizationService.GetLang("新值") + CurValue, ifLang: false);
				InvokeOnParamValueChanged(OldCurValue, CurValue);
			}
			try
			{
				OnPropertyChanged("CurValueA");
			}
			catch (Exception)
			{
				try
				{
					SetPropertyValueByName("CurValueA", OldCurValue);
				}
				catch
				{
				}
			}
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
			EnumEntrys = value;
			OnPropertyChanged("EnumEntrysA");
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

	public virtual bool ReadOnlyA
	{
		get
		{
			return ReadOnly;
		}
		set
		{
			ReadOnly = value;
			OnPropertyChanged("ReadOnlyA");
		}
	}

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

	public bool SubscribeInPopWindowEnableA
	{
		get
		{
			return SubscribeInPopWindowEnable;
		}
		set
		{
			if (SubscribeInPopWindowEnable != value)
			{
				SubscribeInPopWindowEnable = value;
				OnPropertyChanged("SubscribeInPopWindowEnableA");
			}
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

	public ICommand PopSubscribeWindowCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (popSubscribeWindowCommand == null)
			{
				popSubscribeWindowCommand = new DelegateCommand((Action)PopSubscribeWindowCommandExecute);
			}
			return (ICommand)popSubscribeWindowCommand;
		}
	}

	public bool IsSelectedSubscribeParam
	{
		get
		{
			AlgorithmTabRoot algorithmTabRoot = GetAlgorithmTabRoot();
			if (algorithmTabRoot != null)
			{
				return algorithmTabRoot.IsSubscribeInWindow && algorithmTabRoot.SelectedSubscribeParam == this;
			}
			return false;
		}
	}

	public ButtonSelecter()
	{
	}

	public ButtonSelecter(object parent, string type, string relateInput)
	{
		SetParentNode(parent);
		DropOpenSetterA = new Setter
		{
			TargetNameA = "EnumEntrys",
			OperationNameA = "GetFrontFilterTreeOperation",
			OperationParams = type
		};
		DropOpenSetterA.SetParentNode(this);
		Setter setter = new Setter
		{
			OperationNameA = "SetFilterSourceOperation",
			OperationParams = relateInput
		};
		setter.SetParentNode(this);
		Trigger trigger = new Trigger
		{
			Property = "CurValue",
			SettersA = new ObservableCollection<object> { setter }
		};
		trigger.SetParentNode(this);
		TriggersA = new ObservableCollection<object> { trigger };
		Setter setter2 = new Setter
		{
			TargetNameA = "CurValue",
			OperationNameA = "GetSelectedFilterOperation",
			OperationParams = relateInput
		};
		setter2.SetParentNode(this);
		InitersA = new ObservableCollection<object> { setter2 };
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
		AlgorithmTabRoot algorithmTabRoot = GetAlgorithmTabRoot();
		if (algorithmTabRoot != null)
		{
			algorithmTabRoot.IsSubscribeIndex = false;
			if (algorithmTabRoot.IsSubscribeInWindow && SubscribeInPopWindowEnableA)
			{
				algorithmTabRoot.SelectedSubscribeParam = this;
				IsDropDownOpen = false;
				OnPropertyChanged("IsSelectedSubscribeParam");
			}
		}
	}

	protected virtual void ClickCommandExecute(ObjectEventArgs e)
	{
		if (e.Param == null)
		{
			return;
		}
		if (e.Param is VmIO { ModulePathName: var modulePathName })
		{
			if (!string.IsNullOrEmpty(CurValue) && CurValue.Contains('['))
			{
				string text = CurValue.Remove(0, CurValue.IndexOf('['));
				CurValueA = text.Insert(0, modulePathName);
			}
			else
			{
				CurValueA = modulePathName + "[]";
			}
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() == "null")
		{
			CurValueA = null;
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() != "null")
		{
			string displayNameA = (e.Param as Holder).DisplayNameA;
			if (displayNameA.Equals("Lang_VariableManager"))
			{
				VmIO subFilteFromGlobalVariable = GetSubFilteFromGlobalVariable(e.Param);
				if (subFilteFromGlobalVariable != null)
				{
					string modulePathName2 = subFilteFromGlobalVariable.ModulePathName;
					if (subFilteFromGlobalVariable != null)
					{
						selectedFilterType = subFilteFromGlobalVariable.TypeName;
					}
					if (!string.IsNullOrEmpty(CurValue) && CurValue.Contains('['))
					{
						string text2 = CurValue.Remove(0, CurValue.IndexOf('['));
						CurValueA = text2.Insert(0, modulePathName2);
					}
					else
					{
						CurValueA = modulePathName2 + "[]";
					}
				}
			}
			else if ((e.Param as Holder).HolderTarget is Object obj)
			{
				CurValueA = AppLocalizationService.GetLangSet(obj.FullName, '.');
			}
		}
		IsDropDownOpen = false;
	}

	protected VmIO GetSubFilteFromGlobalVariable(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if ((obj as Holder).HolderTarget != null)
		{
			object obj2 = ActivatorWrap.CreateInstance("Apps.XmlUI.ParamTab.GlobalVarDlgHelper");
			string[] array = new string[0];
			bool isInput = false;
			VmModule vmModule = null;
			if ((obj as Holder).HolderTarget is List<object> list)
			{
				if (list.Count < 2)
				{
					return null;
				}
				array = list[0] as string[];
				if (list[1] is bool)
				{
					isInput = (bool)list[1];
				}
				else if (list[1] is VmModule vmModule2)
				{
					vmModule = vmModule2;
				}
			}
			else if ((obj as Holder).HolderTarget is string[] array2)
			{
				array = array2;
			}
			string[] array3 = null;
			array3 = ((array.Length == 0) ? new string[3] { "int", "float", "string" } : array);
			VmModule vmModule3 = ((vmModule != null) ? vmModule : GetVmModule());
			IVmModule localVarModule = vmModule3.GetLocalVarModule(isInput);
			object returnObj = obj2.GetType().InvokeMember("ShowVarSelectDlg", BindingFlags.InvokeMethod, null, obj2, new object[3]
			{
				true,
				(vmModule3 is IVmProcedure) ? null : localVarModule,
				array3
			});
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

	private void IndexClickCommandExecute(ObjectEventArgs e)
	{
		if (e.Param is VmIO vmIO)
		{
			ClearBetweenBracket();
			string value = vmIO.ModulePathName.ToString();
			CurValueA = CurValueA.Insert(curSelectionStart, value);
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() == "null")
		{
			ClearBetweenBracket(isTriger: true);
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() != "null")
		{
			string displayNameA = (e.Param as Holder).DisplayNameA;
			if (displayNameA.Equals("Lang_VariableManager"))
			{
				VmIO subFilteFromGlobalVariable = GetSubFilteFromGlobalVariable(e.Param);
				if (subFilteFromGlobalVariable != null)
				{
					string modulePathName = subFilteFromGlobalVariable.ModulePathName;
					ClearBetweenBracket();
					CurValueA = CurValueA.Insert(curSelectionStart, modulePathName);
				}
			}
		}
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			algorithmTabRoot.IsSubscribeIndex = false;
		}
	}

	private void ClearBetweenBracket(bool isTriger = false)
	{
		try
		{
			if (CurValueA == null || CurValueA.Length < curSelectionStart)
			{
				return;
			}
			int num = curSelectionStart - CurValueA.Substring(0, curSelectionStart).Reverse().ToList()
				.IndexOf('[');
			int num2 = curSelectionStart + CurValueA.Substring(curSelectionStart).IndexOf(']');
			if (CurValueA.Length > num && CurValueA.Length > num2)
			{
				string text = CurValueA.Remove(num, num2 - num);
				if (isTriger)
				{
					CurValueA = text;
				}
				else
				{
					SetPropertyValueByNameNotrigger("CurValueA", text);
				}
				curSelectionStart = num;
			}
		}
		catch
		{
		}
	}

	private void FocusedCommandExecute(TextBoxFocusEventArgs e)
	{
		IsTextPopupVisible = true;
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
				if (IndexOpenSetter.GetParentNode() == null)
				{
					IndexOpenSetter.SetParentNode(this);
				}
				if (IndexOpenSetter.Operation != null)
				{
					IndexOpenSetter.Operate(null);
				}
				curSelectionStart = e.SelectionStart;
			}
			IsTextDropDownOpen = flag;
			AlgorithmTabRoot algorithmTabRoot = GetAlgorithmTabRoot();
			if (algorithmTabRoot != null && algorithmTabRoot.IsSubscribeInWindow)
			{
				algorithmTabRoot.SelectedSubscribeParam = (flag ? this : null);
				IsTextPopupVisible = false;
				algorithmTabRoot.IsSubscribeIndex = flag;
			}
		}
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	private void PopSubscribeWindowCommandExecute()
	{
		AlgorithmTabRoot algorithmTabRoot = GetAlgorithmTabRoot();
		if (algorithmTabRoot != null)
		{
			algorithmTabRoot.SelectedSubscribeParam = this;
			algorithmTabRoot.IsSubscribeInWindow = true;
			OnPropertyChanged("IsSelectedSubscribeParam");
		}
	}

	protected override void ParamItemSelectedCommandExecute()
	{
		AlgorithmTabRoot algorithmTabRoot = GetAlgorithmTabRoot();
		if (algorithmTabRoot == null || !algorithmTabRoot.IsSubscribeInWindow || algorithmTabRoot.IsSubscribeIndex)
		{
			return;
		}
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
		algorithmTabRoot.SelectedSubscribeParam = this;
		OnPropertyChanged("IsSelectedSubscribeParam");
	}

	private AlgorithmTabRoot GetAlgorithmTabRoot()
	{
		if (algorithmTabRoot == null)
		{
			object obj = GetTopBaseNode();
			if (obj is ParamRoot)
			{
				obj = ((obj as ParamRoot).Owner as VmModule)?.Params?.GetParamRoot();
			}
			algorithmTabRoot = obj as AlgorithmTabRoot;
		}
		return algorithmTabRoot;
	}
}
