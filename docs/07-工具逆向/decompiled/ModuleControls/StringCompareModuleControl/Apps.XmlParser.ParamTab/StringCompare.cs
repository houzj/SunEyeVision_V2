using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Apps.Data;
using VM.Controls;
using VM.Core;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class StringCompare : NameValueItem
{
	private const int MaxLen = 512;

	private const string INPUT_NAME = "InputString";

	private int _displayIndex;

	public string CurValue;

	public string DefaultValue;

	public int Length;

	private string _realIndex = "1";

	private ButtonSelecter _inputSelector = null;

	private bool _upenable;

	private bool _downenable;

	private DelegateCommand _upCommand;

	private DelegateCommand _downCommand;

	private DelegateCommand _deleteCommand;

	private AddButtonStringCompare _addButton;

	private DelegateCommand _valueChangedCommand;

	public string InputName => "InputString" + NameA;

	public int DisplayIndexA
	{
		get
		{
			return _displayIndex;
		}
		set
		{
			if (GetParentNode() is Category { ItemsA: var itemsA })
			{
				StringCompare[] array = (from StringCompare p in itemsA.Where((object p) => p is StringCompare)
					where p.CustomVisibleA
					select p).ToArray();
				DownEnable = value < array.Length - 1;
				UpEnable = value > 0;
			}
			if (_displayIndex != value)
			{
				_displayIndex = value;
				OnPropertyChanged("DisplayIndexA");
				Display2Value();
			}
		}
	}

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (!(CurValue == value))
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
				Value2Display();
				SetParamUnit();
			}
		}
	}

	public string RealIndex
	{
		get
		{
			return _realIndex;
		}
		set
		{
			if (!(_realIndex == value))
			{
				int result = 0;
				if (string.IsNullOrEmpty(value))
				{
					value = "0";
				}
				if (int.TryParse(value, out result))
				{
					_realIndex = value;
					OnPropertyChanged("RealIndex");
					Display2Value();
				}
			}
		}
	}

	public ButtonSelecter InputSelector
	{
		get
		{
			if (_inputSelector == null)
			{
				StringCompareDynamicIOContainer stringCompareDynamicIOContainer = null;
				if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
				{
					object obj = algorithmTabRoot.GetInOutputByName("Input");
					if (obj == null)
					{
						obj = (StringCompareDynamicIOContainer)algorithmTabRoot.GetInOutputByName("Input", new StringCompareDynamicIOContainer
						{
							IsOutput = false,
							DataSource = GetVmModule()
						});
					}
					stringCompareDynamicIOContainer = obj as StringCompareDynamicIOContainer;
				}
				if (stringCompareDynamicIOContainer == null)
				{
					return _inputSelector;
				}
				object obj2 = stringCompareDynamicIOContainer.ItemsA.FirstOrDefault((object p) => (p as DynamicIO).NameA == "%" + InputName + "%");
				if (obj2 == null)
				{
					stringCompareDynamicIOContainer.AddIO(InputName, "string");
				}
				_inputSelector = (stringCompareDynamicIOContainer.ItemsA.FirstOrDefault((object p) => (p as DynamicIO).NameA == "%" + InputName + "%") as DynamicIO)?.FilterSelecter;
				if (_inputSelector != null)
				{
					WeakEventManager<ButtonSelecter, EventArgs>.AddHandler(_inputSelector, "OnCurValueChanged", InputSelectorValueChanged);
				}
			}
			_inputSelector?.InniterBusiness();
			return _inputSelector;
		}
	}

	public string DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

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

	public override string Value
	{
		get
		{
			return CurValueA;
		}
		set
		{
			if (!(Value == value))
			{
				CurValueA = value;
			}
		}
	}

	public bool UpEnable
	{
		get
		{
			return _upenable;
		}
		set
		{
			if (_upenable != value)
			{
				_upenable = value;
				OnPropertyChanged("UpEnable");
			}
		}
	}

	public bool DownEnable
	{
		get
		{
			return _downenable;
		}
		set
		{
			if (_downenable != value)
			{
				_downenable = value;
				OnPropertyChanged("DownEnable");
			}
		}
	}

	public ICommand MoveUpCommand => _upCommand ?? (_upCommand = new DelegateCommand(MoveUpCommandExecute));

	public ICommand MoveDownCommand => _downCommand ?? (_downCommand = new DelegateCommand(MoveDownCommandExecute));

	public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(DeleteCommandCmdExecute));

	public override bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
				Display2Value();
				ModifyIndex();
			}
		}
	}

	public ICommand ValueChangedCommand => _valueChangedCommand ?? (_valueChangedCommand = new DelegateCommand(ValueChangedCmdExecute));

	private void MoveUpCommandExecute()
	{
		if (GetParentNode() is Category { ItemsA: var itemsA })
		{
			List<StringCompare> list = (from StringCompare p in itemsA.Where((object p) => p is StringCompare)
				where p.CustomVisibleA
				select p).ToList();
			int num = list.IndexOf(this);
			if (num != 0)
			{
				Swap(list[num - 1]);
			}
		}
	}

	private void Swap(StringCompare swapItem)
	{
		string curValueA = InputSelector.CurValueA;
		string realIndex = RealIndex;
		InputSelector.CurValueA = swapItem.InputSelector.CurValueA;
		RealIndex = swapItem.RealIndex;
		swapItem.InputSelector.CurValueA = curValueA;
		swapItem.RealIndex = realIndex;
	}

	private void MoveDownCommandExecute()
	{
		if (GetParentNode() is Category { ItemsA: var itemsA })
		{
			List<StringCompare> list = (from StringCompare p in itemsA.Where((object p) => p is StringCompare)
				where p.CustomVisibleA
				select p).ToList();
			int num = list.IndexOf(this);
			if (num != list.Count - 1)
			{
				Swap(list[num + 1]);
			}
		}
	}

	private void DeleteCommandCmdExecute()
	{
		if (int.TryParse(RealIndex, out var _))
		{
			RealIndex = "0";
			CustomVisibleA = false;
			if (InputSelector != null)
			{
				InputSelector.CurValueA = "";
			}
		}
	}

	private AddButtonStringCompare GetAddButtonStringCompare()
	{
		if (_addButton != null)
		{
			return _addButton;
		}
		if (GetParentNode() is Category category)
		{
			_addButton = category.ItemsA.OfType<AddButtonStringCompare>().FirstOrDefault();
		}
		return _addButton;
	}

	private void Display2Value()
	{
		if (InputSelector != null)
		{
			string text = $"{DisplayIndexA}-{CustomVisibleA}-{InputSelector.CurValueA}-{RealIndex}";
			if (text.ByteCount() > 512)
			{
				HikMessageBox.Show("Params Settings", "Lang_ParamLenErr", MessageIcons.Warning, isTranslate: true);
			}
			else
			{
				CurValueA = text;
			}
		}
	}

	private void Value2Display()
	{
		if (InputSelector == null)
		{
			return;
		}
		string[] array = CurValue.Split('-');
		if (array != null && array.Count() >= 3 && !string.IsNullOrWhiteSpace(array[0]) && !string.IsNullOrWhiteSpace(array[1]) && array[2] != null && int.TryParse(array[0], out var result) && bool.TryParse(array[1], out var result2))
		{
			DisplayIndexA = result;
			CustomVisibleA = result2;
			InputSelector.CurValueA = ((array[2] == "") ? null : array[2]);
			if (array.Length > 3)
			{
				RealIndex = array[3];
			}
		}
	}

	private void ModifyIndex()
	{
		if (GetParentNode() is Category { ItemsA: var itemsA })
		{
			StringCompare[] array = (from StringCompare p in itemsA.Where((object p) => p is StringCompare)
				where p.CustomVisibleA
				select p).ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				array[num].DisplayIndexA = num;
			}
		}
	}

	public override int GetParamUnit()
	{
		try
		{
			MyItemState = ItemState.Normal;
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return -1;
			}
			StringBuilder stringBuilder = new StringBuilder();
			string text = stringBuilder.ToString();
			text = vmModule.Params[Name];
			if (CurValueA == text)
			{
				Value2Display();
				return 0;
			}
			OldValue = CurValueA;
			CurValueA = text;
			return 0;
		}
		catch (Exception)
		{
			return -1;
		}
	}

	private void InputSelectorValueChanged(object sender, EventArgs e)
	{
		Display2Value();
	}

	private void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			SetParamUnit();
			OldValue = Value;
		}
	}

	public void ClearCurValue()
	{
		string[] array = Value.Split('-');
		if (array != null && array.Count() >= 4 && !string.IsNullOrWhiteSpace(array[0]) && !string.IsNullOrWhiteSpace(array[1]) && array[2] != null && !string.IsNullOrWhiteSpace(array[3]))
		{
			string text = string.Format("{0}-{1}-{2}-{3}", array[0], array[1], "", array[3]);
			if (text.ByteCount() <= 512)
			{
				CurValueA = text;
			}
		}
	}
}
