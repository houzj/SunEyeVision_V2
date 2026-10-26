using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.Interface;

public interface IImageViewBoxWAddition
{
	object splotmROIView { get; }

	object splot { get; }

	object MapROI { get; set; }

	double CurMovePointX { get; }

	double CurMovePointY { get; }

	void ClearPlotShape();

	void CheckShrink();

	void CheckEnlarge();

	void CheckRestore();

	void ClearGraphs();

	void CreatePlotCrossPoint(double x, double y, string color, string fillColor, double Opacity, double StrokeThickness, string Tips);

	void CreatePlotRect(double x, double y, double height, double width, double angle, string color, string fillColor, double Opacity, double StrokeThickness, string Tips);

	void CreatePlotCircle(double x, double y, double height, double width, double angle, string color, string fillColor, double Opacity, double StrokeThickness, string Tips);

	void CreatePlotPolygon(double[] xArg, double[] yArg, string color, string fillColor, double Opacity, double StrokeThickness, string Tips);

	void CreatePlotLine(double x1, double y1, double x2, double y2, string color, string fillColor, double Opacity, double StrokeThickness, string Tips);

	void CreatePlotText(double x1, double y1, string content, double fontsize, string color, string fillColor, double Opacity, double StrokeThickness, string Tips);

	bool SetOutlineImageSource();

	void SetOutlineImageSource(double[] xCoordinates, double[] yCoordinates, Color color);

	void PlotRestore();

	void DrawOnce();

	void UpdateShowROI(object realRoi, object showRoi);

	void UpdateRealROI(object roi);

	object TransRoi(object iROI, bool isPixel, object key = null);

	void ManagerListener(object oldValue, object newValue);

	void SetImageSource3D(bool isDirect = false, bool isInner = false);
}
