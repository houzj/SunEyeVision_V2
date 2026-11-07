namespace Apps.XmlParser.ParamTab;

public class CustomizeOutputParam : NameValueItem
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
