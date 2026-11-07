using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROIPolygon : ROIStructInterface
{
	public uint Num;

	public FloatPoint[] Points;

	public ROIPolygon(uint num)
	{
		Num = num;
		Points = new FloatPoint[Num];
	}

	public IROI GetIROI(ROISelecter rOISelecter, int curImageWidth, int curImageHeight)
	{
		IPolygonROI polygonROI = null;
		polygonROI = ((!(rOISelecter.SelectType == "Avoid")) ? ((IPolygonROI)new NormalizedPolygonROIModel()) : ((IPolygonROI)new NormalizedPolygonMaskROIModel()));
		for (int i = 0; i < Num; i++)
		{
			FloatPoint floatPoint = Points[i];
			polygonROI.PolygonPoints.Add(new Point
			{
				X = floatPoint.X * (float)curImageWidth,
				Y = floatPoint.Y * (float)curImageHeight
			});
		}
		return polygonROI;
	}

	internal void UpdateIROI(IPolygonROI roi)
	{
		for (int i = 0; i < Num; i++)
		{
			FloatPoint floatPoint = Points[i];
			Point value = new Point
			{
				X = floatPoint.X,
				Y = floatPoint.Y
			};
			roi.PolygonPoints[i] = value;
		}
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(rOISelecter, 1, 1);
	}
}
