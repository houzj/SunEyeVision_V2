namespace Apps.XmlParser.ParamTab;

public class EdgeLearningModelData : NameValueItem
{
	private int curValue = 0;

	public int CurValue
	{
		get
		{
			return curValue;
		}
		set
		{
			if (curValue != value)
			{
				curValue = value;
				OnPropertyChanged("CurValue");
			}
		}
	}
}
