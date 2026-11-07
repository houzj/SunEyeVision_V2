using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class ROISelecter_Extern : ROISelecter
{
	public override string DisplayType
	{
		get
		{
			return "ROISelecter";
		}
		set
		{
			base.DisplayType = value;
		}
	}

	protected override void ClearOldROI(IROI newROI)
	{
	}
}
