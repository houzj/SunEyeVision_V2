namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_LoopControl : NameValueItem
{
	public LinkRealItem LinkItemOfType;

	public LinkRealItem LinkItemOfSelecter;

	private Enumeration CmpType;

	private ButtonSelecter Selecter;

	public Enumeration CmpTypeA
	{
		get
		{
			return CmpType;
		}
		set
		{
			if (CmpType != value)
			{
				CmpType = value;
				OnPropertyChanged("CmpTypeA");
			}
		}
	}

	public ButtonSelecter SelecterA
	{
		get
		{
			return Selecter;
		}
		set
		{
			if (Selecter != value)
			{
				Selecter = value;
				OnPropertyChanged("SelecterA");
			}
		}
	}
}
