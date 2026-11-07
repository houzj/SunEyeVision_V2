namespace Apps.XmlParser.ParamTab;

public class ImageViewConfig : BridgeItemBase
{
	public bool PanVisible = true;

	public bool PanVisibleA
	{
		get
		{
			return PanVisible;
		}
		set
		{
			PanVisible = value;
			OnPropertyChanged("PanVisibleA");
		}
	}
}
