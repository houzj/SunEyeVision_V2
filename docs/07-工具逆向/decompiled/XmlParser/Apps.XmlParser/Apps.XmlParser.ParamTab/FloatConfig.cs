namespace Apps.XmlParser.ParamTab;

public class FloatConfig : BridgeItemBase
{
	public double MinValue;

	public double MaxValue;

	public int DecimalDigits = 3;

	public double IncValue = 1.0;

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
}
