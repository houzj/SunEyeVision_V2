namespace Apps.XmlParser.ParamTab;

public class InspectData : NameValueItem
{
	public int CurValue;

	public object Data => this;

	public int CurValueA
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
}
