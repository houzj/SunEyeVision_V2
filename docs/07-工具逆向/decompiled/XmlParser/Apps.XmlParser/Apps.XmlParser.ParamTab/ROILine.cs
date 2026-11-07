using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROILine : ROIStructInterface
{
	public float StartX;

	public float StartY;

	public float EndX;

	public float EndY;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		NormalizedLineROIModel normalizedLineROIModel = new NormalizedLineROIModel();
		normalizedLineROIModel.StartPoint = new Point(StartX * (float)curImageWidth, StartY * (float)curImageHeight);
		normalizedLineROIModel.EndPoint = new Point(EndX * (float)curImageWidth, EndY * (float)curImageHeight);
		return normalizedLineROIModel;
	}

	internal void UpdateIROI(ILineROI roi)
	{
		roi.StartPoint = new Point(StartX, StartY);
		roi.EndPoint = new Point(EndX, EndY);
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
