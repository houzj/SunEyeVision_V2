namespace Apps.XmlParser.ParamTab;

public class GroupHolder : Holder
{
	private bool _IsExpanded;

	public int ID { get; set; }

	public bool IsExpanded
	{
		get
		{
			return _IsExpanded;
		}
		set
		{
			if (_IsExpanded != value)
			{
				_IsExpanded = value;
				OnPropertyChanged("IsExpanded");
			}
		}
	}
}
