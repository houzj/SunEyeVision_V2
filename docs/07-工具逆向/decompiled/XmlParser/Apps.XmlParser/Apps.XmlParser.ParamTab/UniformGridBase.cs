using System.Collections.ObjectModel;
using Apps.Interface;

namespace Apps.XmlParser.ParamTab;

public class UniformGridBase : NameValueItem, IContainerRun
{
	public bool CurValue;

	public ObservableCollection<object> EnumEntrys;

	public bool isControlChildRun { get; set; } = false;

	public bool CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
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
			CurValueA = !string.IsNullOrEmpty(value) && bool.Parse(value);
		}
	}

	public ObservableCollection<object> EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
			OnPropertyChanged("EnumEntrysA");
		}
	}

	public override int GetParamUnit()
	{
		foreach (object item in EnumEntrysA)
		{
			if (item is BooleanCheck booleanCheck)
			{
				booleanCheck.GetParamUnit();
			}
		}
		int paramUnit = base.GetParamUnit();
		if (paramUnit == 0)
		{
			OldValue = Value;
		}
		return paramUnit;
	}
}
