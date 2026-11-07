using System;
using System.Collections.Generic;
using System.Windows.Input;
using Apps.Data;
using Hik.Xml;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ConditionItem : NameValueItem
{
	public ButtonSelecter _FilterSelecter;

	public FloatBettween _FilterBettween;

	public IntegerBettween _integerBettween;

	public string _TypeName;

	public string _ConditionGroupName;

	public int _ConditionGroupID;

	public int _ConditionID;

	private ICommand _DeleteConditionItemCommand = null;

	public VmModule vmModule { get; set; }

	public object owner { get; set; }

	public ButtonSelecter FilterSelecter
	{
		get
		{
			if (_FilterSelecter == null)
			{
				FilterSelecterInit();
			}
			return _FilterSelecter;
		}
	}

	public FloatBettween FilterBettween
	{
		get
		{
			if (_FilterBettween == null)
			{
				FilterBettweenInit();
			}
			return _FilterBettween;
		}
	}

	public IntegerBettween IntegerBettween
	{
		get
		{
			if (_integerBettween == null)
			{
				FilterIntBettweenInit();
			}
			return _integerBettween;
		}
	}

	public string TypeName
	{
		get
		{
			return _TypeName;
		}
		set
		{
			if (_TypeName != value)
			{
				_TypeName = value;
				OnPropertyChanged("TypeName");
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

	public int ConditionID
	{
		get
		{
			return _ConditionID;
		}
		set
		{
			if (_ConditionID != value)
			{
				_ConditionID = value;
				OnPropertyChanged("ConditionID");
			}
		}
	}

	public Dictionary<string, string> BettweenSubData => vmModule.GetPropertyValueByName("BettweenSubData") as Dictionary<string, string>;

	public ICommand DeleteConditionItemCommand
	{
		get
		{
			if (_DeleteConditionItemCommand == null)
			{
				_DeleteConditionItemCommand = (ICommand)new DelegateCommand<object>((Action<object>)DeleteConditionItemCommandExe);
			}
			return _DeleteConditionItemCommand;
		}
	}

	public void FilterSelecterInit()
	{
		BaseNode baseNode = GroupsRoot.GetInstance()["SubscriptionFilterDataFilterByIO"];
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					_FilterSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		if (_FilterSelecter != null)
		{
			DataFilterConfig dataFilterConfig = (owner as ConditionGroupItem).owner as DataFilterConfig;
			_FilterSelecter.SetParentNode(dataFilterConfig);
			_FilterSelecter.DropOpenSetter.OperationParams = TypeName;
			_FilterSelecter.InniterBusiness();
			_FilterSelecter.ReadOnlyA = true;
			_FilterSelecter.OnCurValueChanged -= FilterSelecterParamValueChanged;
			_FilterSelecter.OnCurValueChanged += FilterSelecterParamValueChanged;
		}
	}

	public void FilterSelecterParamValueChanged(object sender, EventArgs e)
	{
		((owner as ConditionGroupItem).owner as DataFilterConfig).SetParamUnitCondition();
	}

	public void FilterBettweenInit()
	{
		_FilterBettween = new FloatBettween();
		_FilterBettween.SetParentNode(this);
		if (_FilterBettween.Floats != null)
		{
			return;
		}
		Float obj = null;
		Float obj2 = null;
		_FilterBettween.MaxValueA = 9999999.0;
		_FilterBettween.MinValueA = -9999999.0;
		obj = new Float();
		obj.AccessModeA = "R";
		obj.NameA = ConditionGroupID + "-" + ConditionID + "-Start";
		obj.MaxValueA = 9999999.0;
		obj.MinValueA = -9999999.0;
		obj.CurValueA = 1.0;
		obj2 = new Float();
		obj2.AccessModeA = "R";
		obj2.NameA = ConditionGroupID + "-" + ConditionID + "-End";
		obj2.MaxValueA = 9999999.0;
		obj2.MinValueA = -9999999.0;
		obj2.CurValueA = 1000.0;
		obj.DecimalDigitsA = 2;
		obj2.DecimalDigitsA = 2;
		_FilterBettween.DecimalDigitsA = 2;
		obj.SetParentNode(_FilterBettween);
		obj2.SetParentNode(_FilterBettween);
		if (BettweenSubData != null)
		{
			if (BettweenSubData.ContainsKey(obj.NameA))
			{
				string text = BettweenSubData[obj.NameA];
				if (text != null)
				{
					obj.IsSubA = true;
					obj.SubData.CurValue = text;
				}
			}
			if (BettweenSubData.ContainsKey(obj2.NameA))
			{
				string text2 = BettweenSubData[obj2.NameA];
				if (text2 != null)
				{
					obj2.IsSubA = true;
					obj2.SubData.CurValue = text2;
				}
			}
		}
		obj.OnCurValueChanged += CurValueChanged;
		obj2.OnCurValueChanged += CurValueChanged;
		obj.OnIsSubChanged += IsSubChanged;
		obj2.OnIsSubChanged += IsSubChanged;
		obj.SubData.OnCurValueChanged += SubCurValueChanged;
		obj2.SubData.OnCurValueChanged += SubCurValueChanged;
		_FilterBettween.FloatsA = new List<object>();
		_FilterBettween.FloatsA.Add(obj);
		_FilterBettween.FloatsA.Add(obj2);
	}

	public void FilterIntBettweenInit()
	{
		if (_integerBettween != null)
		{
			return;
		}
		_integerBettween = new IntegerBettween();
		_integerBettween.SetParentNode(this);
		if (_integerBettween.Integers != null)
		{
			return;
		}
		Integer integer = null;
		Integer integer2 = null;
		_integerBettween.MaxValueA = 9999999;
		_integerBettween.MinValueA = -9999999;
		integer = new Integer();
		integer.AccessModeA = "R";
		integer.NameA = ConditionGroupID + "-" + ConditionID + "-Start";
		integer.MaxValueA = 9999999;
		integer.MinValueA = -9999999;
		integer.CurValueA = 1;
		integer2 = new Integer();
		integer2.AccessModeA = "R";
		integer2.NameA = ConditionGroupID + "-" + ConditionID + "-End";
		integer2.MaxValueA = 9999999;
		integer2.MinValueA = -9999999;
		integer2.CurValueA = 1000;
		integer.SetParentNode(_integerBettween);
		integer2.SetParentNode(_integerBettween);
		integer.OnCurValueChanged += CurValueChanged;
		integer2.OnCurValueChanged += CurValueChanged;
		integer.OnIsSubChanged += IsSubChanged;
		integer2.OnIsSubChanged += IsSubChanged;
		if (BettweenSubData != null)
		{
			if (BettweenSubData.ContainsKey(integer.NameA))
			{
				string text = BettweenSubData[integer.NameA];
				if (text != null)
				{
					integer.IsSubA = true;
					integer.SubData.CurValue = text;
				}
			}
			if (BettweenSubData.ContainsKey(integer2.NameA))
			{
				string text2 = BettweenSubData[integer2.NameA];
				if (text2 != null)
				{
					integer2.IsSubA = true;
					integer2.SubData.CurValue = text2;
				}
			}
		}
		integer.SubData.OnCurValueChanged += SubCurValueChanged;
		integer2.SubData.OnCurValueChanged += SubCurValueChanged;
		_integerBettween.IntegersA = new List<object>();
		_integerBettween.IntegersA.Add(integer);
		_integerBettween.IntegersA.Add(integer2);
	}

	private void CurValueChanged(object sender, EventArgs e)
	{
		((owner as ConditionGroupItem).owner as DataFilterConfig).SetParamUnitCondition();
	}

	private void IsSubChanged(bool isSub)
	{
		((owner as ConditionGroupItem).owner as DataFilterConfig).SetParamUnitCondition();
	}

	private void SubCurValueChanged(object sender, EventArgs e)
	{
		if (BettweenSubData != null)
		{
			ButtonSelecter buttonSelecter = sender as ButtonSelecter;
			string nameA = (buttonSelecter.GetParentNode() as ValueBase).NameA;
			if (BettweenSubData.ContainsKey(nameA))
			{
				BettweenSubData[nameA] = buttonSelecter.CurValue;
			}
			else
			{
				BettweenSubData.Add(nameA, buttonSelecter.CurValue);
			}
		}
	}

	public void DeleteConditionItemCommandExe(object obj)
	{
		if (obj is ConditionItem { owner: ConditionGroupItem conditionGroupItem } conditionItem)
		{
			foreach (Float item in conditionItem.FilterBettween.FloatsA)
			{
				item.IsSubA = false;
				item.SubData.CurValueA = null;
				if (conditionItem?.BettweenSubData != null && conditionItem.BettweenSubData.ContainsKey(item.Name))
				{
					conditionItem.BettweenSubData.Remove(item.Name);
				}
			}
			conditionGroupItem.ConditionItems.Remove(conditionItem);
		}
		((owner as ConditionGroupItem).owner as DataFilterConfig).SetParamUnitCondition();
	}
}
