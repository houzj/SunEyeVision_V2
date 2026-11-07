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
	public ButtonSelecter _ClassificationSelecter;

	public FloatBettween _ClassificationBettween;

	public IntegerBettween _IntegerBettween;

	public string _TypeName;

	public string _ConditionGroupName;

	public int _ConditionGroupID;

	public int _ConditionID;

	private ICommand _DeleteConditionItemCommand = null;

	public VmModule vmModule { get; set; }

	public DataClassificationConfig config { get; set; }

	public ConditionGroupItem owner { get; set; }

	public ClassificationItem classItem { get; set; }

	public ButtonSelecter ClassificationSelecter
	{
		get
		{
			if (_ClassificationSelecter == null)
			{
				ClassificationSelecterInit();
			}
			return _ClassificationSelecter;
		}
	}

	public FloatBettween ClassificationBettween
	{
		get
		{
			if (_ClassificationBettween == null)
			{
				ClassificationBettweenInit();
			}
			return _ClassificationBettween;
		}
	}

	public IntegerBettween IntegerBettween
	{
		get
		{
			if (_IntegerBettween == null)
			{
				ClassificationIntBettweenInit();
			}
			return _IntegerBettween;
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
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteConditionItemCommand == null)
			{
				_DeleteConditionItemCommand = (ICommand)new DelegateCommand((Action)DeleteConditionItemCommandExe);
			}
			return _DeleteConditionItemCommand;
		}
	}

	public void ClassificationSelecterInit()
	{
		BaseNode baseNode = GroupsRoot.GetInstance()["SubscriptionFilterDataClassificationByIO"];
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					_ClassificationSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		if (_ClassificationSelecter != null)
		{
			_ClassificationSelecter.SetParentNode(this);
			_ClassificationSelecter.DropOpenSetter.OperationParams = TypeName;
			_ClassificationSelecter.InniterBusiness();
			_ClassificationSelecter.ReadOnlyA = true;
			_ClassificationSelecter.OnCurValueChanged -= ClassificationSelecterParamValueChanged;
			_ClassificationSelecter.OnCurValueChanged += ClassificationSelecterParamValueChanged;
		}
	}

	public void ClassificationSelecterParamValueChanged(object sender, EventArgs e)
	{
		if (config != null && classItem != null)
		{
			config.SetConditionItem(classItem);
		}
	}

	public void ClassificationIntBettweenInit()
	{
		if (_IntegerBettween != null)
		{
			return;
		}
		_IntegerBettween = new IntegerBettween();
		_IntegerBettween.SetParentNode(this);
		if (_IntegerBettween.Integers != null)
		{
			return;
		}
		Integer integer = null;
		Integer integer2 = null;
		_IntegerBettween.MaxValueA = 9999999;
		_IntegerBettween.MinValueA = -9999999;
		integer = new Integer();
		integer.AccessModeA = "R";
		integer.NameA = classItem.ClassificationName + "-" + ConditionGroupID + "-" + ConditionID + "-Start";
		integer.MaxValueA = 9999999;
		integer.MinValueA = -9999999;
		integer.CurValueA = 1;
		integer2 = new Integer();
		integer2.AccessModeA = "R";
		integer2.NameA = classItem.ClassificationName + "-" + ConditionGroupID + "-" + ConditionID + "-End";
		integer2.MaxValueA = 9999999;
		integer2.MinValueA = -9999999;
		integer2.CurValueA = 1000;
		integer.SetParentNode(_IntegerBettween);
		integer2.SetParentNode(_IntegerBettween);
		integer.OnCurValueChanged += CurValueChanged;
		integer2.OnCurValueChanged += CurValueChanged;
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
		_IntegerBettween.IntegersA = new List<object>();
		_IntegerBettween.IntegersA.Add(integer);
		_IntegerBettween.IntegersA.Add(integer2);
	}

	public void ClassificationBettweenInit()
	{
		_ClassificationBettween = new FloatBettween();
		_ClassificationBettween.SetParentNode(this);
		if (_ClassificationBettween.Floats != null)
		{
			return;
		}
		Float obj = null;
		Float obj2 = null;
		_ClassificationBettween.MaxValueA = 9999999.0;
		_ClassificationBettween.MinValueA = -9999999.0;
		obj = new Float();
		obj.AccessModeA = "R";
		obj.NameA = classItem.ClassificationName + "-" + ConditionGroupID + "-" + ConditionID + "-Start";
		obj.MaxValueA = 9999999.0;
		obj.MinValueA = -9999999.0;
		obj.CurValueA = 1.0;
		obj2 = new Float();
		obj2.AccessModeA = "R";
		obj2.NameA = classItem.ClassificationName + "-" + ConditionGroupID + "-" + ConditionID + "-End";
		obj2.MaxValueA = 9999999.0;
		obj2.MinValueA = -9999999.0;
		obj2.CurValueA = 1000.0;
		obj.DecimalDigitsA = 2;
		obj2.DecimalDigitsA = 2;
		_ClassificationBettween.DecimalDigitsA = 2;
		obj.SetParentNode(_ClassificationBettween);
		obj2.SetParentNode(_ClassificationBettween);
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
		obj.SubData.OnCurValueChanged += SubCurValueChanged;
		obj2.SubData.OnCurValueChanged += SubCurValueChanged;
		obj.OnIsSubChanged += IsSubChanged;
		obj2.OnIsSubChanged += IsSubChanged;
		_ClassificationBettween.FloatsA = new List<object>();
		_ClassificationBettween.FloatsA.Add(obj);
		_ClassificationBettween.FloatsA.Add(obj2);
	}

	private void CurValueChanged(object sender, EventArgs e)
	{
		if (config != null && classItem != null)
		{
			config.SetConditionItem(classItem);
		}
	}

	private void IsSubChanged(bool isSub)
	{
		if (config != null && classItem != null)
		{
			config.SetConditionItem(classItem);
		}
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

	public void DeleteConditionItemCommandExe()
	{
		if (BettweenSubData == null || owner == null || config == null || classItem == null)
		{
			return;
		}
		if (TypeName == "int")
		{
			foreach (Integer integer in IntegerBettween.Integers)
			{
				integer.IsSubA = false;
				integer.SubData.CurValueA = null;
				if (BettweenSubData.ContainsKey(integer.Name))
				{
					BettweenSubData.Remove(integer.Name);
				}
			}
		}
		else
		{
			foreach (Float @float in _ClassificationBettween.Floats)
			{
				@float.IsSubA = false;
				@float.SubData.CurValueA = null;
				if (BettweenSubData.ContainsKey(@float.Name))
				{
					BettweenSubData.Remove(@float.Name);
				}
			}
		}
		owner.ConditionItems.Remove(this);
		config.SetConditionItem(classItem);
	}

	public void DeleteBettweenSubData()
	{
		if (BettweenSubData == null)
		{
			return;
		}
		if (TypeName == "int")
		{
			foreach (Integer integer in IntegerBettween.Integers)
			{
				integer.IsSubA = false;
				integer.SubData.CurValueA = null;
				if (BettweenSubData.ContainsKey(integer.Name))
				{
					BettweenSubData.Remove(integer.Name);
				}
			}
			return;
		}
		foreach (Float @float in _ClassificationBettween.Floats)
		{
			@float.IsSubA = false;
			@float.SubData.CurValueA = null;
			if (BettweenSubData.ContainsKey(@float.Name))
			{
				BettweenSubData.Remove(@float.Name);
			}
		}
	}
}
