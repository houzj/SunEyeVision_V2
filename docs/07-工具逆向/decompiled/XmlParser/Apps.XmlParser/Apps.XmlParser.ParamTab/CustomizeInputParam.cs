namespace Apps.XmlParser.ParamTab;

public class CustomizeInputParam : NameValueItem
{
	public string CommandName;

	public string CommandNameA
	{
		get
		{
			return CommandName;
		}
		set
		{
			CommandName = value;
			OnPropertyChanged("CommandNameA");
		}
	}
}
