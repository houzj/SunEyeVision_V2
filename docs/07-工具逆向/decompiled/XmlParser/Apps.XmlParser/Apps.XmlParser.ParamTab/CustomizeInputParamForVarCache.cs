namespace Apps.XmlParser.ParamTab;

public class CustomizeInputParamForVarCache : NameValueItem
{
	public string CommandName;

	public string AddPointCommandName;

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

	public string AddPointCommandNameA
	{
		get
		{
			return AddPointCommandName;
		}
		set
		{
			AddPointCommandName = value;
			OnPropertyChanged("AddPointCommandNameA");
		}
	}
}
