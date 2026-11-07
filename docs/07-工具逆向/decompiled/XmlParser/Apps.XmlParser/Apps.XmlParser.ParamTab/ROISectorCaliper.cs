using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROISectorCaliper : ROIStructInterface
{
	public float StartX;

	public float StartY;

	public float EndX;

	public float EndY;

	public float Radius;

	public float CaliperHeight;

	public float CaliperWidth;

	public uint CaliperNum;

	public uint IsLargeArc;

	public uint IsClockwise;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		NormalizedSectorCaliperROIModel normalizedSectorCaliperROIModel = new NormalizedSectorCaliperROIModel();
		normalizedSectorCaliperROIModel.StartPoint = new Point(StartX * (float)curImageWidth, StartY * (float)curImageHeight);
		normalizedSectorCaliperROIModel.EndPoint = new Point(EndX * (float)curImageWidth, EndY * (float)curImageHeight);
		normalizedSectorCaliperROIModel.Radius = Radius * (float)curImageWidth;
		normalizedSectorCaliperROIModel.CaliperWidth = CaliperWidth * (float)curImageHeight;
		normalizedSectorCaliperROIModel.CaliperHeight = CaliperHeight * (float)curImageWidth;
		normalizedSectorCaliperROIModel.CaliperNum = CaliperNum;
		normalizedSectorCaliperROIModel.IsLargeArc = IsLargeArc == 1;
		normalizedSectorCaliperROIModel.IsClockwise = IsClockwise == 1;
		return normalizedSectorCaliperROIModel;
	}

	internal void UpdateIROI(ISectorCaliperROI roi)
	{
		roi.StartPoint = new Point(StartX, StartY);
		roi.EndPoint = new Point(EndX, EndY);
		roi.Radius = Radius;
		roi.CaliperWidth = CaliperWidth;
		roi.CaliperHeight = CaliperHeight;
		roi.CaliperNum = CaliperNum;
		roi.IsLargeArc = IsLargeArc == 1;
		roi.IsClockwise = IsClockwise == 1;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
