using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public class IntegerBettween : ValueBettween
{
	public int MinValue;

	public int MaxValue;

	public int IncValue = 1;

	public List<object> Integers;

	public List<object> IntegersA
	{
		get
		{
			return Integers;
		}
		set
		{
			if (Integers != value)
			{
				Integers = value;
			}
			OnPropertyChanged("IntegersA");
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
			MinValue = value;
			OnPropertyChanged("MinValueA");
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
			MaxValue = value;
			OnPropertyChanged("MaxValueA");
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
			IncValue = value;
			OnPropertyChanged("IncValueA");
		}
	}

	public override IList<object> GetSubChildren()
	{
		return Integers;
	}

	internal override void SetValueToStore(ValueBettweenStore dataStore)
	{
		dataStore.start = (SubChildren[0] as Integer).CurValue;
		dataStore.end = (SubChildren[1] as Integer).CurValue;
	}

	internal override void GetValueToStore(ValueBettweenStore store)
	{
		(SubChildren[0] as Integer).CurValue = (int)store.start;
		(SubChildren[1] as Integer).CurValue = (int)store.end;
	}
}
