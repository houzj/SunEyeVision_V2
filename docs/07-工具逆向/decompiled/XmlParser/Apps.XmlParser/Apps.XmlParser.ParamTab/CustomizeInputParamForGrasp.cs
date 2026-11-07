namespace Apps.XmlParser.ParamTab;

public class CustomizeInputParamForGrasp : NameValueItem
{
	public string CommandName;

	public string DelCommandName;

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

	public string DelCommandNameA
	{
		get
		{
			return DelCommandName;
		}
		set
		{
			DelCommandName = value;
			OnPropertyChanged("DelCommandNameA");
		}
	}
}
