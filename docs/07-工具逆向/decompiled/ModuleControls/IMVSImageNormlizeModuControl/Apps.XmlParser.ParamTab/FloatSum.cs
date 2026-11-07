using System;
using System.Collections.Generic;
using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class FloatSum : NameValueItem, ISubChild, ISubInfoCollection
{
	public double MinValue;

	public double MaxValue;

	public double CurValue;

	public double DefaultValue;

	public double IncValue = 1.0;

	public double Sum;

	public List<object> Floats;

	public double SumA => Sum;

	public List<object> FloatsA
	{
		get
		{
			return Floats;
		}
		set
		{
			if (Floats != value)
			{
				Floats = value;
			}
		}
	}

	public double CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
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

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			CurValueA = int.Parse(value);
		}
	}

	public IList<object> SubChildren => Floats;

	public override int SetParamUnit()
	{
		NameValueItem nameValueItem = null;
		MyItemState = ItemState.Normal;
		if (Floats != null && Floats.Count > 1)
		{
			for (int i = 0; i < 2; i++)
			{
				if (Floats[i] is NameValueItem nameValueItem2)
				{
					int num = nameValueItem2.SetParamUnit();
					if (num == -1)
					{
						SetChildState(nameValueItem2.MyItemState);
						return num;
					}
				}
			}
		}
		return 0;
	}

	private void SetChildState(ItemState itemState)
	{
		if (itemState != ItemState.Normal)
		{
			MyItemState = itemState;
		}
	}

	public override int GetParamUnit()
	{
		NameValueItem nameValueItem = null;
		if (Floats != null && Floats.Count > 1)
		{
			for (int i = 0; i < 2; i++)
			{
				if (Floats[i] is NameValueItem nameValueItem2)
				{
					int paramUnit = nameValueItem2.GetParamUnit();
					if (paramUnit == -1)
					{
						SetChildState(nameValueItem2.MyItemState);
						return paramUnit;
					}
				}
			}
		}
		return 0;
	}

	public Float GetTheOtherFloat(Float floatItem)
	{
		if (Floats == null || Floats.Count != 2)
		{
			return null;
		}
		int num = -1;
		for (int i = 0; i < 2; i++)
		{
			if (Floats[i] is Float obj && obj.Name == floatItem.Name)
			{
				num = ((i == 0) ? 1 : 0);
				break;
			}
		}
		if (num >= 0 && num < 2)
		{
			return Floats[num] as Float;
		}
		return null;
	}

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}
}
