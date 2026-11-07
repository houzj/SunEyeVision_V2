using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROICircleCaliper : ROIStructInterface
{
	public float CenterX;

	public float CenterY;

	public float MaxRadius;

	public float StartAngle;

	public float EndAngle;

	public float CaliperHeight;

	public float CaliperWidth;

	public uint CaliperNum;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		NormalizedCircleCaliperROIModel normalizedCircleCaliperROIModel = new NormalizedCircleCaliperROIModel();
		normalizedCircleCaliperROIModel.CenterX = CenterX * (float)curImageWidth;
		normalizedCircleCaliperROIModel.CenterY = CenterY * (float)curImageHeight;
		normalizedCircleCaliperROIModel.Radius = MaxRadius * (float)curImageWidth;
		normalizedCircleCaliperROIModel.CaliperWidth = CaliperWidth * (float)curImageHeight;
		normalizedCircleCaliperROIModel.CaliperHeight = CaliperHeight * (float)curImageWidth;
		normalizedCircleCaliperROIModel.CaliperNum = CaliperNum;
		normalizedCircleCaliperROIModel.StartAngle = StartAngle;
		return normalizedCircleCaliperROIModel;
	}

	internal void UpdateIROI(ICircleCaliperROI roi)
	{
		roi.CenterPoint = new Point(CenterX, CenterY);
		roi.Radius = MaxRadius;
		roi.CaliperWidth = CaliperWidth;
		roi.CaliperHeight = CaliperHeight;
		roi.CaliperNum = CaliperNum;
		roi.StartAngle = StartAngle;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
