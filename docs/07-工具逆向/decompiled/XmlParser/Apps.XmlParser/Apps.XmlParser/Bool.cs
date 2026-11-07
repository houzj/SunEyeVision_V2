namespace Apps.XmlParser;

public class Bool : BaseParamNode
{
	public int CurValue;

	public int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}
}
