using System;
using System.Collections.Generic;
using Apps.Data;

namespace Apps.XmlParser.ParamTab;

public class PointF : NameValueItem, ISubChild, ISubInfoCollection
{
	public double MinValue;

	public double MaxValue;

	public double CurValue;

	public double DefaultValue;

	public double IncValue = 1.0;

	public int DecimalDigits = 3;

	public List<object> Floats;

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
			OnPropertyChanged("FloatsA");
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
			CurValueA = int.Parse(value);
		}
	}

	public IList<object> SubChildren
	{
		get
		{
			if (Floats != null && Floats.Count > 1)
			{
				for (int i = 0; i < 3; i++)
				{
					NameValueItem nameValueItem = Floats[i] as NameValueItem;
					if (string.IsNullOrEmpty(nameValueItem.NameA))
					{
						switch (i)
						{
						case 0:
							nameValueItem.NameA = NameA + ".X";
							break;
						case 1:
							nameValueItem.NameA = NameA + ".Y";
							break;
						case 2:
							nameValueItem.NameA = NameA + ".R";
							break;
						}
					}
				}
			}
			return Floats;
		}
	}

	public PointF()
	{
		Floats = new List<object>();
		Floats.Add(new Float
		{
			MinValue = -9999.0,
			MaxValue = 9999.0,
			IncValue = 0.001,
			DecimalDigits = 3
		});
		Floats.Add(new Float
		{
			MinValue = -9999.0,
			MaxValue = 9999.0,
			IncValue = 0.001,
			DecimalDigits = 3
		});
		Floats.Add(new Float
		{
			MinValue = -360.0,
			MaxValue = 360.0,
			IncValue = 0.001,
			DecimalDigits = 3
		});
		foreach (object @float in Floats)
		{
			(@float as Float).SetParentNode(this);
		}
	}

	public override int SetParamUnit()
	{
		NameValueItem nameValueItem = null;
		MyItemState = ItemState.Normal;
		if (Floats != null && Floats.Count > 1)
		{
			for (int i = 0; i < 3; i++)
			{
				nameValueItem = Floats[i] as NameValueItem;
				if (string.IsNullOrEmpty(nameValueItem.NameA))
				{
					switch (i)
					{
					case 0:
						nameValueItem.NameA = NameA + ".X";
						break;
					case 1:
						nameValueItem.NameA = NameA + ".Y";
						break;
					case 2:
						nameValueItem.NameA = NameA + ".R";
						break;
					}
				}
				if (nameValueItem != null)
				{
					int num = nameValueItem.SetParamUnit();
					if (num == -1)
					{
						SetChildState(nameValueItem.MyItemState);
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
			for (int i = 0; i < 3; i++)
			{
				nameValueItem = Floats[i] as NameValueItem;
				if (string.IsNullOrEmpty(nameValueItem.NameA))
				{
					switch (i)
					{
					case 0:
						nameValueItem.NameA = NameA + ".X";
						break;
					case 1:
						nameValueItem.NameA = NameA + ".Y";
						break;
					case 2:
						nameValueItem.NameA = NameA + ".R";
						break;
					}
				}
				if (nameValueItem != null)
				{
					int paramUnit = nameValueItem.GetParamUnit();
					if (paramUnit == -1)
					{
						SetChildState(nameValueItem.MyItemState);
						return paramUnit;
					}
				}
			}
		}
		return 0;
	}

	public NameValueItem GetChildByName(string name)
	{
		throw new NotImplementedException();
	}
}
