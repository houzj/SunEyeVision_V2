using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROIMatrixCircle : ROIStructInterface
{
	public float CenterX;

	public float CenterY;

	public float Width;

	public float Height;

	public float Angle;

	public uint M;

	public uint N;

	public float MaxRadius;

	public float MinRadius;

	public IROI GetIROI(int curImageWidth, int curImageHeight)
	{
		return new NormalizedMatrixCircleROIModel
		{
			CenterPoint = new Point(CenterX * (float)curImageWidth, CenterY * (float)curImageHeight),
			Width = Width * (float)curImageWidth,
			Height = Height * (float)curImageHeight,
			Angle = Angle * (float)curImageHeight,
			Radius = MaxRadius * (float)curImageWidth,
			MinRadius = MinRadius * (float)curImageWidth,
			M = M,
			N = N
		};
	}

	internal void UpdateIROI(IMatrixCircleROI roi)
	{
		roi.CenterPoint = new Point(CenterX, CenterY);
		roi.Height = Height;
		roi.Width = Width;
		roi.Angle = Angle;
		roi.M = M;
		roi.N = N;
		roi.Radius = MaxRadius;
		roi.MinRadius = MinRadius;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1);
	}
}
