using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROICicleMes : ROIStructInterface
{
	public float CenterX;

	public float CenterY;

	public float Radius;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		NormalizedCircleMesROIModel normalizedCircleMesROIModel = new NormalizedCircleMesROIModel();
		normalizedCircleMesROIModel.CenterPoint = new Point(CenterX * (float)curImageWidth, CenterY * (float)curImageHeight);
		normalizedCircleMesROIModel.Radius = Radius * (float)curImageWidth;
		return normalizedCircleMesROIModel;
	}

	internal void UpdateIROI(ICircleMesROI roi)
	{
		roi.CenterPoint = new Point(CenterX, CenterY);
		roi.Radius = Radius;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
