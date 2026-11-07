namespace Apps.XmlParser.ParamTab;

public class CustomizeInputParamForMeasure : NameValueItem
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
