using System;
using System.Xml;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class NumberMultiply : NameValueItem
{
	public string CurValue;

	public string DefaultValue;

	public int LeftValue;

	public int RightValue;

	private int TBorderThickness;

	public int MinValue = 1;

	public int MaxValue = 100;

	public int IncValue = 1;

	private const int MVBSTITCH_MIN_IMG_NUM = 2;

	private const int MVBSTITCH_MAX_IMG_NUM = 100;

	internal bool needUpdateImageNumber = true;

	private const int maxWaitTime = 300;

	private bool isGettingParam = false;

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value || !CheckValueChanged(LeftValue, RightValue))
			{
				CurValue = value;
				if (!isGettingParam)
				{
					SetParamUnit();
				}
				string[] array = CurValue.Split('X');
				if (array.Length == 2)
				{
					LeftValueA = int.Parse(array[0]);
					RightValueA = int.Parse(array[1]);
				}
				OnPropertyChanged("CurValueA");
			}
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
			CurValueA = value;
		}
	}

	public int LeftValueA
	{
		get
		{
			return LeftValue;
		}
		set
		{
			if (LeftValue != value)
			{
				LeftValue = value;
				OnPropertyChanged("LeftValueA");
				if (CheckValueChanged(value, RightValueA) && !isGettingParam)
				{
					UpdateImageNumber();
					CurValueA = LeftValueA + "X" + RightValueA;
				}
			}
		}
	}

	public int RightValueA
	{
		get
		{
			return RightValue;
		}
		set
		{
			if (RightValue != value)
			{
				RightValue = value;
				OnPropertyChanged("RightValueA");
				if (CheckValueChanged(LeftValueA, value) && !isGettingParam)
				{
					UpdateImageNumber();
					CurValueA = LeftValueA + "X" + RightValueA;
				}
			}
		}
	}

	public int Product => LeftValueA * RightValueA;

	public int TBorderThicknessA
	{
		get
		{
			return TBorderThickness;
		}
		set
		{
			if (TBorderThickness != value)
			{
				TBorderThickness = value;
				OnPropertyChanged("TBorderThicknessA");
			}
		}
	}

	public int MinValueA
	{
		get
		{
			return MinValue;
		}
		set
		{
			if (MinValue != value)
			{
				MinValue = value;
				OnPropertyChanged("MinValueA");
			}
		}
	}

	public int MaxValueA
	{
		get
		{
			return MaxValue;
		}
		set
		{
			if (MaxValue != value)
			{
				MaxValue = value;
				OnPropertyChanged("MaxValueA");
			}
		}
	}

	public int IncValueA
	{
		get
		{
			return IncValue;
		}
		set
		{
			if (IncValue != value)
			{
				IncValue = value;
				OnPropertyChanged("IncValueA");
			}
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		CheckValueChanged(LeftValue, RightValue);
		return this;
	}

	private bool CheckValueChanged(int left, int right)
	{
		int num = left * right;
		if (num < 2 || num > 100)
		{
			TBorderThicknessA = 1;
			return false;
		}
		TBorderThicknessA = 0;
		return true;
	}

	public void UpdateImageNumber()
	{
		if (!needUpdateImageNumber || !(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot) || !(algorithmTabRoot.FindBaseNodeByName("ImageNum") is Integer integer))
		{
			return;
		}
		try
		{
			integer.CurValueA = Product;
			integer.SetParamUnit();
		}
		catch (Exception ex)
		{
			throw new Exception(ex.Message);
		}
	}

	public override int GetParamUnit()
	{
		isGettingParam = true;
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("r"))
		{
			OnGetValueCompleted(success: false);
			isGettingParam = false;
			return -2;
		}
		if (OldValue != null && !CheckValueChanged(LeftValue, RightValue))
		{
			CurValueA = OldValue;
		}
		int paramUnitByZmq = GetParamUnitByZmq();
		isGettingParam = false;
		return paramUnitByZmq;
	}
}
