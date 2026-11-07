using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Apps.Localization;
using Apps.XmlParser.ParamTab;
using HikUtil.Release;
using VM.Controls;
using VM.Core;
using VM.Utility;

namespace CalculatorModuleControl;

public class CalculatorItem : INotifyPropertyChanged, IDisposable
{
	private const int MaxLen = 32;

	private const string NamePattern = "^[\\u4e00-\\u9fa5_a-zA-Z0-9]+$";

	private string _type = "float";

	private string _name;

	private string _expression;

	private float _initial;

	private bool _summation;

	private string _remarkInfo = string.Empty;

	internal ButtonSelecter_CalculatorPro _parent;

	internal VmIO vmIO;

	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			if (!(_name == value))
			{
				if (string.IsNullOrEmpty(value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamNameErr"), AppLocalizationService.GetLang("Lang_PmValueEptErr"), MessageIcons.Error);
				}
				else if (value.ByteCount() > 32)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamNameErr"), AppLocalizationService.GetLang("Lang_PmNameLong"), MessageIcons.Error);
				}
				else if (!Regex.IsMatch(value, "^[\\u4e00-\\u9fa5_a-zA-Z0-9]+$"))
				{
					string lang = AppLocalizationService.GetLang("Lang_IlgChar");
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamNameErr"), lang, MessageIcons.Error);
				}
				else if (_parent != null && _parent.Items.Any((CalculatorItem p) => p.Name == value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamNameErr"), AppLocalizationService.GetLang("Lang_PmNameExited"), MessageIcons.Error);
				}
				else
				{
					_name = value;
					OnPropertyChanged("Name");
				}
			}
		}
	}

	public string Expression
	{
		get
		{
			return _expression;
		}
		set
		{
			if (!(_expression == value))
			{
				_expression = value;
				OnPropertyChanged("Expression");
			}
		}
	}

	public float Initial
	{
		get
		{
			return _initial;
		}
		set
		{
			if (_initial != value)
			{
				_initial = value;
				OnPropertyChanged("Initial");
			}
		}
	}

	public string Type
	{
		get
		{
			return _type;
		}
		set
		{
			if (!(_type == value))
			{
				_type = value;
				OnPropertyChanged("Type");
				OnPropertyChanged("CanEdit");
				if (_type == "POINT")
				{
					Summation = false;
					Initial = 0f;
				}
			}
		}
	}

	public bool Summation
	{
		get
		{
			return _summation;
		}
		set
		{
			if (_summation != value)
			{
				_summation = value;
				OnPropertyChanged("Summation");
			}
		}
	}

	public bool CanEdit => Type != "POINT";

	public string RemarkInfo
	{
		get
		{
			return _remarkInfo;
		}
		set
		{
			if (value != _remarkInfo)
			{
				if (StringToByteCount(value) > 256)
				{
					HikMessageBox.Show("Lang_ParamNameErr", "Lang_ParamLenErr", MessageIcons.Error, isTranslate: true);
					return;
				}
				_remarkInfo = value;
				OnPropertyChanged("RemarkInfo");
			}
		}
	}

	public int Index { get; set; }

	public event PropertyChangedEventHandler PropertyChanged;

	public CalculatorItem(ButtonSelecter_CalculatorPro parent)
	{
		_parent = parent;
	}

	public CalculatorItem()
	{
	}

	public CalculatorItem(ButtonSelecter_CalculatorPro parent, CalculatorItem calculatorItem)
	{
		_parent = parent;
		Index = calculatorItem.Index;
		_expression = calculatorItem.Expression;
		_initial = calculatorItem.Initial;
		_name = calculatorItem.Name;
		_summation = calculatorItem.Summation;
		_type = calculatorItem.Type;
		_remarkInfo = calculatorItem.RemarkInfo;
	}

	private void OnPropertyChanged(string propertyname)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyname));
		}
	}

	public void Dispose()
	{
		if (_parent == null)
		{
			return;
		}
		if (_parent.Items.Contains(this))
		{
			if (_parent.Items.Count == 1)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Params Settings"), AppLocalizationService.GetLang("Lang_PmuNeedOne"), MessageIcons.Warning);
				return;
			}
			_parent.Items.Remove(this);
		}
		_parent = null;
		HikUtil.Release.ReleaseHelper.RemoveEvents(this);
	}

	public ButtonSelecter_CalculatorPro GetParent()
	{
		return _parent;
	}

	public static int StringToByteCount(string input)
	{
		return (!string.IsNullOrEmpty(input)) ? (Encoding.UTF8.GetByteCount(input) + 1) : 0;
	}

	public override bool Equals(object obj)
	{
		if (obj is CalculatorItem calculatorItem)
		{
			if (calculatorItem.Name == Name && calculatorItem.Expression == Expression && calculatorItem.Initial == Initial && calculatorItem.Summation == Summation && calculatorItem.Index == Index && calculatorItem.Type == Type && calculatorItem.RemarkInfo == RemarkInfo)
			{
				return true;
			}
			return false;
		}
		return false;
	}
}
