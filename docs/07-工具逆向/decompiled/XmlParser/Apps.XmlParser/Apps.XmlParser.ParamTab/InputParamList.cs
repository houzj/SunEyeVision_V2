namespace Apps.XmlParser.ParamTab;

public class InputParamList : NameValueItem
{
	public string AddParamCommandName;

	public string AddParamCommandNameA
	{
		get
		{
			return AddParamCommandName;
		}
		set
		{
			AddParamCommandName = value;
			OnPropertyChanged("AddParamCommandNameA");
		}
	}
}
