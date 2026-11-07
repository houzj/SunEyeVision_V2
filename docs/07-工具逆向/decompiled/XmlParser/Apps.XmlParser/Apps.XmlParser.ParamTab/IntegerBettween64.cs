using System.Collections.Generic;

namespace Apps.XmlParser.ParamTab;

public class IntegerBettween64 : ValueBettween
{
	public long MinValue;

	public long MaxValue;

	public long IncValue = 1L;

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

	public override IList<object> GetSubChildren()
	{
		return Integers;
	}

	internal override void SetValueToStore(ValueBettweenStore dataStore)
	{
		dataStore.start = (SubChildren[0] as Integer64).CurValue;
		dataStore.end = (SubChildren[1] as Integer64).CurValue;
	}

	internal override void GetValueToStore(ValueBettweenStore store)
	{
		(SubChildren[0] as Integer64).CurValue = (int)store.start;
		(SubChildren[1] as Integer64).CurValue = (int)store.end;
	}
}
