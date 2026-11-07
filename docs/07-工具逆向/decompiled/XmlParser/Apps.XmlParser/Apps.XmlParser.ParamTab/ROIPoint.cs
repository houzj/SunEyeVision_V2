using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROIPoint : ROIStructInterface
{
	public float X;

	public float Y;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		NormalizedPointROIModel normalizedPointROIModel = new NormalizedPointROIModel();
		normalizedPointROIModel.CenterPoint = new Point(X * (float)curImageWidth, Y * (float)curImageHeight);
		return normalizedPointROIModel;
	}

	internal void UpdateIROI(IPointROI roi)
	{
		roi.CenterPoint = new Point(X, Y);
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
