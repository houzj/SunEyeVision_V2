using System.Collections;

namespace Apps.XmlParser.ParamTab;

public class EnumerationG : NameValueItem
{
	private string CurValue;

	private IList EnumEntrys;

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (value != null)
			{
				int num = value.IndexOf('[', 0);
				if (num > 0)
				{
					value = value.Remove(num);
				}
			}
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				CurValue = null;
				EnumEntrys = value;
			}
			OnPropertyChanged("EnumEntrysA");
		}
	}

	public override bool NeedInit => false;

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
