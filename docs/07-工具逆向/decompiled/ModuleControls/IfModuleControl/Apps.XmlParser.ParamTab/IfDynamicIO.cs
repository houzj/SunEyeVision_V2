using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Apps.Log;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class IfDynamicIO : DynamicIO
{
	internal const int MaxNameLen = 256;

	private string _selectType;

	private ButtonSelecter _FilterSelecterIf;

	private FloatBettween _floatbetween;

	private IntegerBettween _integerBettween;

	private string _remarkInfo;

	public List<string> SelectValueBasicTypes => new List<string> { "int", "float" };

	public string SelectType
	{
		get
		{
			return _selectType;
		}
		set
		{
			if (_selectType != value)
			{
				_selectType = value;
				OnPropertyChanged("SelectType");
			}
		}
	}

	public int VisiableCount => Enumerable.Count((GetParentNode() as IfModuleDynamicIO).ItemsA, (object x) => (x as IfDynamicIO).CustomVisibleA);

	public ButtonSelecter FilterSelecterIf
	{
		get
		{
			if (_FilterSelecterIf == null)
			{
				_FilterSelecterIf = new ButtonSelecter();
				if (CombineType.Contains(ValueTypeA))
				{
					_FilterSelecterIf.DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontCombinationTreeOperation"
					};
					_FilterSelecterIf.DropOpenSetter.SetParentNode(_FilterSelecterIf);
					_FilterSelecterIf.SetParentNode(this);
					_FilterSelecterIf.Triggers = new ObservableCollection<object>();
					Trigger trigger = new Trigger
					{
						Property = "CurValue"
					};
					Setter setter = new Setter
					{
						OperationName = "SetCombinationSourceOperation",
						OperationParams = NameA
					};
					setter.CreatOperateInstance();
					trigger.Setters = new ObservableCollection<object>();
					trigger.Setters.Add(setter);
					setter.SetParentNode(trigger);
					_FilterSelecterIf.Triggers.Add(trigger);
					trigger.SetParentNode(_FilterSelecterIf);
					_FilterSelecterIf.Initers = new ObservableCollection<object>();
					Setter setter2 = new Setter
					{
						TargetName = "CurValue",
						OperationName = "GetSelectedCombinationOperation",
						OperationParams = NameA
					};
					setter2.CreatOperateInstance();
					_FilterSelecterIf.Initers.Add(setter2);
					setter2.SetParentNode(_FilterSelecterIf);
				}
				else
				{
					_FilterSelecterIf.DropOpenSetter = new Setter
					{
						TargetName = "EnumEntrys",
						OperationName = "GetFrontFilterTreeOperation"
					};
					_FilterSelecterIf.DropOpenSetter.SetParentNode(_FilterSelecterIf);
					_FilterSelecterIf.SetParentNode(this);
					_FilterSelecterIf.Triggers = new ObservableCollection<object>();
					Trigger trigger2 = new Trigger
					{
						Property = "CurValue"
					};
					Setter setter3 = new Setter
					{
						OperationName = "SetFilterSourceOperation",
						OperationParams = NameA
					};
					setter3.CreatOperateInstance();
					trigger2.Setters = new ObservableCollection<object>();
					trigger2.Setters.Add(setter3);
					setter3.SetParentNode(trigger2);
					_FilterSelecterIf.Triggers.Add(trigger2);
					trigger2.SetParentNode(_FilterSelecterIf);
					_FilterSelecterIf.Initers = new ObservableCollection<object>();
					Setter setter4 = new Setter
					{
						TargetName = "CurValue",
						OperationName = "GetSelectedFilterOperation",
						OperationParams = NameA
					};
					setter4.CreatOperateInstance();
					_FilterSelecterIf.Initers.Add(setter4);
					setter4.SetParentNode(_FilterSelecterIf);
				}
			}
			if (_FilterSelecterIf.DropOpenSetter.OperationParams != null && _FilterSelecterIf.DropOpenSetter.OperationParams != FilterTypeA)
			{
				_FilterSelecterIf.CurValueA = "";
			}
			_FilterSelecterIf.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = NameA;
			_FilterSelecterIf.Initers.OfType<Setter>().First().OperationParams = NameA;
			_FilterSelecterIf.InniterBusiness();
			_FilterSelecterIf.DropOpenSetter.OperationParams = FilterTypeA;
			return _FilterSelecterIf;
		}
	}

	public FloatBettween Floatbetween
	{
		get
		{
			if (_floatbetween == null)
			{
				_floatbetween = new FloatBettween();
				_floatbetween.SetParentNode(this);
				if (_floatbetween.Floats == null)
				{
					Float obj = null;
					Float obj2 = null;
					if (NameA.Contains("int"))
					{
						_floatbetween.MaxValueA = 999999999.0;
						_floatbetween.MinValueA = -999999999.0;
						obj = new Float();
						obj.AccessModeA = "R";
						obj.NameA = NameA + "Start";
						obj.MaxValueA = 999999999.0;
						obj.MinValueA = -999999999.0;
						obj.CurValueA = 1.0;
						obj2 = new Float();
						obj2.AccessModeA = "R";
						obj2.NameA = NameA + "End";
						obj2.MaxValueA = 999999999.0;
						obj2.MinValueA = -999999999.0;
						obj2.CurValueA = 1000.0;
					}
					else
					{
						_floatbetween.MaxValueA = 100000.0;
						_floatbetween.MinValueA = -100000.0;
						obj = new Float();
						obj.AccessModeA = "R";
						obj.NameA = NameA + "Start";
						obj.MaxValueA = 100000.0;
						obj.MinValueA = -100000.0;
						obj.CurValueA = 1.0;
						obj2 = new Float();
						obj2.AccessModeA = "R";
						obj2.NameA = NameA + "End";
						obj2.MaxValueA = 100000.0;
						obj2.MinValueA = -100000.0;
						obj2.CurValueA = 1000.0;
					}
					obj.DecimalDigitsA = (GetParentNode() as IfModuleDynamicIO).DecimalDigits;
					obj2.DecimalDigitsA = (GetParentNode() as IfModuleDynamicIO).DecimalDigits;
					_floatbetween.DecimalDigitsA = (GetParentNode() as IfModuleDynamicIO).DecimalDigits;
					obj.SetParentNode(_floatbetween);
					obj2.SetParentNode(_floatbetween);
					obj.OnCurValueChanged += CurValueChanged;
					obj2.OnCurValueChanged += CurValueChanged;
					_floatbetween.FloatsA = new List<object>();
					_floatbetween.FloatsA.Add(obj);
					_floatbetween.FloatsA.Add(obj2);
				}
			}
			return _floatbetween;
		}
	}

	public IntegerBettween IntegerBettween
	{
		get
		{
			if (_integerBettween == null)
			{
				_integerBettween = new IntegerBettween();
				_integerBettween.SetParentNode(this);
				if (_integerBettween.Integers == null)
				{
					Integer integer = null;
					Integer integer2 = null;
					if (NameA.Contains("int"))
					{
						_integerBettween.MaxValueA = 999999999;
						_integerBettween.MinValueA = -999999999;
						integer = new Integer();
						integer.AccessModeA = "R";
						integer.NameA = NameA + "Start";
						integer.MaxValueA = 999999999;
						integer.MinValueA = -999999999;
						integer.CurValueA = 1;
						integer2 = new Integer();
						integer2.AccessModeA = "R";
						integer2.NameA = NameA + "End";
						integer2.MaxValueA = 999999999;
						integer2.MinValueA = -999999999;
						integer2.CurValueA = 1000;
					}
					else
					{
						_integerBettween.MaxValueA = 100000;
						_integerBettween.MinValueA = -100000;
						integer = new Integer();
						integer.AccessModeA = "R";
						integer.NameA = NameA + "Start";
						integer.MaxValueA = 100000;
						integer.MinValueA = -100000;
						integer.CurValueA = 1;
						integer2 = new Integer();
						integer2.AccessModeA = "R";
						integer2.NameA = NameA + "End";
						integer2.MaxValueA = 100000;
						integer2.MinValueA = -100000;
						integer2.CurValueA = 1000;
					}
					integer.SetParentNode(_integerBettween);
					integer2.SetParentNode(_integerBettween);
					integer.OnCurValueChanged += CurValueChanged;
					integer2.OnCurValueChanged += CurValueChanged;
					_integerBettween.IntegersA = new List<object>();
					_integerBettween.IntegersA.Add(integer);
					_integerBettween.IntegersA.Add(integer2);
				}
			}
			return _integerBettween;
		}
	}

	public IList<NameValueItem> CurrentValueBettweenValue
	{
		get
		{
			if (SelectType == "int")
			{
				return IntegerBettween.Integers.Cast<NameValueItem>().ToList();
			}
			return Floatbetween.Floats.Cast<NameValueItem>().ToList();
		}
	}

	public string RemarkInfo
	{
		get
		{
			return _remarkInfo;
		}
		set
		{
			if (_remarkInfo != value)
			{
				if (value.Count() > 256)
				{
					HikMessageBox.Show("Lang_ParamNameErr", "Lang_ParamLenErr", MessageIcons.Error, isTranslate: true);
					return;
				}
				if (value.Contains(";") || value.Contains(":"))
				{
					HikMessageBox.Show("Lang_ParamNameErr", "参数中不可包含“:”或者“;”", MessageIcons.Error, isTranslate: true);
					return;
				}
				_remarkInfo = value;
				OnPropertyChanged("RemarkInfo");
				SetParamUnit();
			}
		}
	}

	private void CurValueChanged(object sender, EventArgs e)
	{
		SetParamUnit();
	}

	public override int SetParamUnit()
	{
		if ((GetParentNode() as DynamicIOContainer<IfDynamicIO>).IsOutput)
		{
			return 0;
		}
		if ((GetParentNode() as IfModuleDynamicIO).IsAllowSet)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (IfDynamicIO item in (GetParentNode() as DynamicIOContainer<IfDynamicIO>).ItemsA)
			{
				if (item.CustomVisibleA)
				{
					stringBuilder.Append(item.NameA);
					stringBuilder.Append(":");
					stringBuilder.Append(item.CurrentValueBettweenValue[0].Value);
					stringBuilder.Append(":");
					stringBuilder.Append(item.CurrentValueBettweenValue[1].Value);
					stringBuilder.Append(":");
					stringBuilder.Append(item.RemarkInfo);
					stringBuilder.Append(";");
				}
			}
			if (string.IsNullOrEmpty(stringBuilder.ToString()))
			{
				stringBuilder = stringBuilder.Append("0");
			}
			try
			{
				((IUserBytesData)IoDataSource.Root.Params)["IFText"] = StringUtil.UTF8GetBytesPadZero(stringBuilder.ToString());
			}
			catch (Exception ex)
			{
				LogHelper.Error("设置IFText失败：" + ex.Message);
				throw ex;
			}
		}
		return 0;
	}
}
