using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROICoordinate : ROIStructInterface
{
	public float OrgX;

	public float OrgY;

	public float Angle;

	internal IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		return new CoordinateModel
		{
			OrgPoint = new Point(OrgX, OrgY),
			Angle = Angle
		};
	}

	internal void UpdateIROI(ICoordinateROI roi)
	{
		roi.OrgPoint = new Point(OrgX, OrgY);
		roi.Angle = Angle;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
