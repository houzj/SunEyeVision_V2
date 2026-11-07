using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public class FloatBettween : ValueBettween
{
	public double MinValue;

	public double MaxValue;

	public double CurValue;

	public double DefaultValue;

	public double IncValue = 1.0;

	public int DecimalDigits = 2;

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

	public override IList<object> GetSubChildren()
	{
		return Floats;
	}

	internal override void SetValueToStore(ValueBettweenStore dataStore)
	{
		dataStore.start = (SubChildren[0] as Float).CurValue;
		dataStore.end = (SubChildren[1] as Float).CurValue;
	}

	internal override void GetValueToStore(ValueBettweenStore store)
	{
		(SubChildren[0] as Float).CurValue = (double)store.start;
		(SubChildren[1] as Float).CurValue = (double)store.end;
	}
}
