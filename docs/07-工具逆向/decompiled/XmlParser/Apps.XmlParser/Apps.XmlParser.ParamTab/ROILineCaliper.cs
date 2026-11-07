using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROILineCaliper : ROIStructInterface
{
	public float StartX;

	public float StartY;

	public float EndX;

	public float EndY;

	public float CaliperWidth;

	public float CaliperHeight;

	public float CaliperAngle;

	public uint CaliperNum;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		NormalizedLineCaliperROIModel normalizedLineCaliperROIModel = new NormalizedLineCaliperROIModel();
		normalizedLineCaliperROIModel.StartPoint = new Point(StartX * (float)curImageWidth, StartY * (float)curImageHeight);
		normalizedLineCaliperROIModel.EndPoint = new Point(EndX * (float)curImageWidth, EndY * (float)curImageHeight);
		normalizedLineCaliperROIModel.CaliperWidth = CaliperWidth * (float)curImageWidth;
		normalizedLineCaliperROIModel.CaliperHeight = CaliperHeight * (float)curImageHeight;
		normalizedLineCaliperROIModel.CaliperNum = CaliperNum;
		normalizedLineCaliperROIModel.CaliperRotateAngle = CaliperAngle;
		return normalizedLineCaliperROIModel;
	}

	internal void UpdateIROI(ILineCaliperROI roi)
	{
		roi.StartPoint = new Point(StartX, StartY);
		roi.EndPoint = new Point(EndX, EndY);
		roi.CaliperWidth = CaliperWidth;
		roi.CaliperHeight = CaliperHeight;
		roi.CaliperNum = CaliperNum;
		roi.CaliperRotateAngle = CaliperAngle;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
