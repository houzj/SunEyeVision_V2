using System;
using System.Windows;
using Apps.Data;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public struct ROISectorRing : ROIStructInterface
{
	public float CenterX;

	public float CenterY;

	public float MinRadius;

	public float MaxRadius;

	public float StartAngle;

	public float EndAngle;

	public IROI GetIROI(int curImageWidth, int curImageHeight, byte version)
	{
		if (StartAngle == EndAngle)
		{
			NormalizedCircleROIModel normalizedCircleROIModel = new NormalizedCircleROIModel();
			normalizedCircleROIModel.CenterX = CenterX * (float)curImageWidth;
			normalizedCircleROIModel.CenterY = CenterY * (float)curImageHeight;
			normalizedCircleROIModel.Radius = MaxRadius * (float)curImageWidth;
			normalizedCircleROIModel.MinRadius = MinRadius * (float)curImageWidth;
			return normalizedCircleROIModel;
		}
		if (version == 1)
		{
			NormalizedOverSectorROIModel normalizedOverSectorROIModel = new NormalizedOverSectorROIModel();
			normalizedOverSectorROIModel.CenterX = CenterX * (float)curImageWidth;
			normalizedOverSectorROIModel.CenterY = CenterY * (float)curImageHeight;
			normalizedOverSectorROIModel.Radius = MaxRadius * (float)curImageWidth;
			normalizedOverSectorROIModel.MinRadius = MinRadius * (float)curImageWidth;
			normalizedOverSectorROIModel._StartAngle = StartAngle;
			normalizedOverSectorROIModel._AngleRange = EndAngle;
			return normalizedOverSectorROIModel;
		}
		NormalizedSectorROIModel normalizedSectorROIModel = new NormalizedSectorROIModel();
		normalizedSectorROIModel.CenterX = CenterX * (float)curImageWidth;
		normalizedSectorROIModel.CenterY = CenterY * (float)curImageHeight;
		normalizedSectorROIModel.Radius = MaxRadius * (float)curImageWidth;
		normalizedSectorROIModel.MinRadius = MinRadius * (float)curImageWidth;
		normalizedSectorROIModel.StartAngle = StartAngle;
		normalizedSectorROIModel.EndAngle = EndAngle;
		return normalizedSectorROIModel;
	}

	internal void UpdateIROI(IROI roi)
	{
		if (roi is ICircleROI circleROI)
		{
			circleROI.CenterPoint = new Point(CenterX, CenterY);
			circleROI.Radius = MaxRadius;
			circleROI.MinRadius = MinRadius;
		}
		else if (roi is ISectorROI sectorROI)
		{
			sectorROI.CenterPoint = new Point(CenterX, CenterY);
			sectorROI.Radius = MaxRadius;
			sectorROI.MinRadius = MinRadius;
			sectorROI.StartAngle = StartAngle;
			sectorROI.EndAngle = EndAngle;
		}
		else if (roi is IOverSectorROI overSectorROI)
		{
			overSectorROI.CenterPoint = new Point(CenterX, CenterY);
			overSectorROI.Radius = MaxRadius;
			overSectorROI.MinRadius = MinRadius;
			overSectorROI.StartAngle = StartAngle;
			overSectorROI.AngleRange = Math.Abs(EndAngle);
			overSectorROI.IsClockwise = true;
		}
	}

	public IROI GetNormalizeIROI(ROISelecter rOISelecter, byte version, bool isSingleRec)
	{
		return GetIROI(1, 1, version);
	}
}
