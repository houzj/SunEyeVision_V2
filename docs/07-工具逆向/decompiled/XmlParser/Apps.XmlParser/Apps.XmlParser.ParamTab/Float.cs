using System;
using Apps.Log;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class Float : ValueBase
{
	private bool isInitValue = false;

	public double MinValue;

	public double MaxValue;

	public double CurValue;

	public double DefaultValue;

	public double IncValue = 1.0;

	public int DecimalDigits = 2;

	public bool NeedSlider = true;

	public override string ComType { get; set; } = "float";

	public bool NeedSliderA
	{
		get
		{
			return NeedSlider;
		}
		set
		{
			NeedSlider = value;
		}
	}

	public virtual double CurValueA
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
				CurValue = Math.Round(value, DecimalDigits);
				isInitValue = true;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public double DefaultValueA
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

	public double MinValueA
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

	public double MaxValueA
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

	public double IncValueA
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

	public int DecimalDigitsA
	{
		get
		{
			return DecimalDigits;
		}
		set
		{
			DecimalDigits = value;
			OnPropertyChanged("DecimalDigitsA");
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
			CurValueA = double.Parse(value);
		}
	}

	public override void Check(NameValueItem parent, string value)
	{
		RangeChecker rangeChecker = new RangeChecker();
		double num = MinValue;
		double num2 = MaxValue;
		if (parentNode is FloatBettween floatBettween && floatBettween.FloatsA.Count == 2)
		{
			int num3 = floatBettween.FloatsA.IndexOf(this);
			if (num3 == 1)
			{
				Float obj = floatBettween.FloatsA[0] as Float;
				if (obj.CurValueA <= num2)
				{
					num = obj.CurValueA;
				}
			}
			else
			{
				Float obj2 = floatBettween.FloatsA[1] as Float;
				double result = 0.0;
				if (obj2.isInitValue)
				{
					result = obj2.CurValueA;
				}
				else
				{
					try
					{
						IUserStringData userStringData = (GetTopBaseNode() as AlgorithmTabRoot).UserStringData;
						string text = null;
						text = ((parent == null) ? userStringData[obj2.ComName] : parent.InnerGetStringParamValue(obj2.ComName));
						double.TryParse(text, out result);
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

	public void ValueChangedExecute()
	{
		ValueChangedCmdExecute();
	}

	protected override void ReSetCurValueA()
	{
		CurValueA = DefaultValueA + 1.0;
		CurValueA = DefaultValueA;
	}
}
