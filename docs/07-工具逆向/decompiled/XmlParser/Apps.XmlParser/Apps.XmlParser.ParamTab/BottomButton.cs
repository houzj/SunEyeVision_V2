namespace Apps.XmlParser.ParamTab;

public class BottomButton : NameValueItem
{
	public string ClickMethod;

	public string ClickMethodA
	{
		get
		{
			return ClickMethod;
		}
		set
		{
			if (ClickMethod != value)
			{
				ClickMethod = value;
				OnPropertyChanged("ClickMethodA");
			}
		}
	}
}
