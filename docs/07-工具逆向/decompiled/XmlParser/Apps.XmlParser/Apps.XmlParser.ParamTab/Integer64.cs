using System;
using Apps.Log;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class Integer64 : ValueBase
{
	private bool isInitValue = false;

	public long MinValue;

	public long MaxValue;

	public long IncValue = 1L;

	public long CurValue;

	public long DefaultValue;

	public string Unit;

	public virtual long CurValueA
	{
		get
		{
			if (string.IsNullOrEmpty(OldValue))
			{
				OldValue = CurValue.ToString();
			}
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				if (value > MaxValue || value < MinValue)
				{
					CurValue = value;
				}
				else
				{
					long num = (value - MinValue) / IncValue;
					CurValue = num * IncValue + MinValue;
				}
				if (isInniter)
				{
					NotifyChanges();
				}
				isInitValue = true;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public long DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
			OnPropertyChanged("DefaultValueA");
		}
	}

	public string UnitA
	{
		get
		{
			return Unit;
		}
		set
		{
			Unit = value;
			OnPropertyChanged("UnitA");
		}
	}

	public long MinValueA
	{
		get
		{
			return MinValue;
		}
		set
		{
			MinValue = value;
			OnPropertyChanged("MinValueA");
		}
	}

	public long MaxValueA
	{
		get
		{
			return MaxValue;
		}
		set
		{
			MaxValue = value;
			OnPropertyChanged("MaxValueA");
		}
	}

	public long IncValueA
	{
		get
		{
			return IncValue;
		}
		set
		{
			IncValue = value;
			OnPropertyChanged("IncValueA");
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			long curValueA = long.Parse(value);
			CurValueA = curValueA;
		}
	}

	public override void Check(NameValueItem parent, string value)
	{
		RangeChecker rangeChecker = new RangeChecker();
		long num = MinValue;
		long num2 = MaxValue;
		if (parentNode is IntegerBettween64 integerBettween && integerBettween.IntegersA.Count == 2)
		{
			int num3 = integerBettween.IntegersA.IndexOf(this);
			if (num3 == 1)
			{
				Integer64 integer = integerBettween.IntegersA[0] as Integer64;
				if (integer.CurValueA <= num2)
				{
					num = integer.CurValueA;
				}
			}
			else
			{
				Integer64 integer2 = integerBettween.IntegersA[1] as Integer64;
				long result = 0L;
				if (integer2.isInitValue)
				{
					result = integer2.CurValueA;
				}
				else
				{
					try
					{
						IUserStringData userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
						string text = null;
						text = ((parent == null) ? userStringData[integer2.ComName] : parent.InnerGetStringParamValue(integer2.ComName));
						long.TryParse(text, out result);
					}
					catch (Exception ex)
					{
						result = MaxValue;
						LogHelper.Error(ex.Message);
					}
				}
				if (result >= num)
				{
					num2 = result;
				}
			}
		}
		rangeChecker.Check(new object[2] { num, num2 }, value, DisplayNameA);
	}

	public override void Check(string value)
	{
		Check(null, value);
	}

	protected override void ReSetCurValueA()
	{
		CurValueA = DefaultValueA + 1;
		CurValueA = DefaultValueA;
	}
}
