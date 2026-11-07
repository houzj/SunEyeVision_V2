using System.Collections.ObjectModel;

namespace Apps.XmlParser.ParamTab;

public class HeadBar : NameValueItem
{
	public ObservableCollection<object> EnumEntrys;

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
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
