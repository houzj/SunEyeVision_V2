namespace Apps.XmlParser.ParamTab;

public class VMGroupOutputParam : NameValueItem
{
	public string AddParamCommandName;

	public string AddPointFCommandName;

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

	public string AddPointFCommandNameA
	{
		get
		{
			return AddPointFCommandName;
		}
		set
		{
			AddPointFCommandName = value;
			OnPropertyChanged("AddPointFCommandNameA");
		}
	}
}
