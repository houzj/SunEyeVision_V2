using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class ROISelecter_Fix : ROISelecter
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

	protected override void ValueChangedCmdExecute(object type)
	{
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot && algorithmTabRoot.FindBaseNodeByName("ExternRoiType") is ROISelecter rOISelecter)
		{
			rOISelecter.SelectNone();
		}
		if (type is RoiType roiType)
		{
			switch (roiType)
			{
			case RoiType.QuadraBox:
				SelectNone();
				SetDefault();
				break;
			case RoiType.PolygonCaliper:
				SelectNone();
				break;
			}
		}
	}

	public void InitROI(IROI roiParameter)
	{
		InitRoi(roiParameter);
	}
}
