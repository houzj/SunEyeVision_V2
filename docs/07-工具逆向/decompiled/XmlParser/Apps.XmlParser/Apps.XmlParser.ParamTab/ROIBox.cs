using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROIBox : ROIStructInterface
{
	public float CenterX;

	public float CenterY;

	public float Width;

	public float Height;

	public float Angle;

	internal const byte UnRotatable = 1;

	public IROI GetIROI(byte version, int curImageWidth, int curImageHeight, bool isLineRect, bool isSeqRect)
	{
		if (version == 1)
		{
			NormalizedUroRectROIModel normalizedUroRectROIModel = new NormalizedUroRectROIModel();
			normalizedUroRectROIModel.Width = Width;
			normalizedUroRectROIModel.Height = Height;
			normalizedUroRectROIModel.CenterPoint = new Point(CenterX, CenterY);
			return normalizedUroRectROIModel;
		}
		if (isLineRect)
		{
			NormalizedLineRectROIModel normalizedLineRectROIModel = new NormalizedLineRectROIModel();
			normalizedLineRectROIModel.Width = Width * (float)curImageWidth;
			normalizedLineRectROIModel.Height = Height * (float)curImageHeight;
			normalizedLineRectROIModel.CenterPoint = new Point(CenterX * (float)curImageWidth, CenterY * (float)curImageHeight);
			normalizedLineRectROIModel.Angle = Angle;
			return normalizedLineRectROIModel;
		}
		if (isSeqRect)
		{
			NormalizedSeqRectROIModel normalizedSeqRectROIModel = new NormalizedSeqRectROIModel();
			normalizedSeqRectROIModel.Width = Width * (float)curImageWidth;
			normalizedSeqRectROIModel.Height = Height * (float)curImageHeight;
			normalizedSeqRectROIModel.CenterPoint = new Point(CenterX * (float)curImageWidth, CenterY * (float)curImageHeight);
			normalizedSeqRectROIModel.Angle = Angle;
			return normalizedSeqRectROIModel;
		}
		NormalizedRectROIModel normalizedRectROIModel = new NormalizedRectROIModel();
		normalizedRectROIModel.Width = Width;
		normalizedRectROIModel.Height = Height;
		normalizedRectROIModel.CenterPoint = new Point(CenterX, CenterY);
		normalizedRectROIModel.Angle = Angle;
		return normalizedRectROIModel;
	}

	public void UpdateIROI(IRectROI roi)
	{
		roi.Width = Width;
		roi.Height = Height;
		roi.CenterPoint = new Point(CenterX, CenterY);
		roi.Angle = Angle;
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(version, 1, 1, rOISelecter.ROISelectionA.Contains(RoiType.LineBox.ToString()), isSingleRec && rOISelecter.ROISelectionA.Contains(RoiType.SeqBox.ToString()));
	}
}
