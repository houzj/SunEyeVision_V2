using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class PolylineShape : ShapeControlBase
{
	public static ShapeControlBase GetPolylineGeometry(IPolyline polyline, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		PolylineShape polylineShape = new PolylineShape();
		List<Point> polylinePointList = polyline.PolylinePointList;
		if (polylinePointList == null || ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		GetInnerGeometry(polylineShape.geometry, polylinePointList, ControlToImageRadio);
		polylineShape.Stroke = DrawShapeHelper.GetBrushByString(polyline.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = polyline.ToolTip;
		toolTip.StaysOpen = true;
		polylineShape.ToolTip = toolTip;
		polylineShape.Opacity = polyline.Opacity;
		polylineShape.Fill = DrawShapeHelper.GetBrushByString(polyline.Fill);
		polylineShape.IsHitTestVisible = !string.IsNullOrEmpty(polyline.ToolTip);
		polylineShape.StrokeThickness = polyline.StrokeThickness / ThicknessScale;
		polylineShape.BaseStrokeThickness = polyline.StrokeThickness;
		return polylineShape;
	}

	private static PathGeometry GetInnerGeometry(PathGeometry geometry, List<Point> points, double ControlToImageRadio)
	{
		PathGeometry pathGeometry = geometry ?? new PathGeometry();
		int count = points.Count;
		for (int i = 0; i < count; i++)
		{
			Point point = points[i];
			if (i + 1 < count)
			{
				Point point2 = points[i + 1];
				double x = (point.X + 0.0) * ControlToImageRadio;
				double y = (point.Y + 0.0) * ControlToImageRadio;
				double x2 = (point2.X + 0.0) * ControlToImageRadio;
				double y2 = (point2.Y + 0.0) * ControlToImageRadio;
				Point startPoint = new Point(x, y);
				Point endPoint = new Point(x2, y2);
				pathGeometry.AddGeometry(new LineGeometry(startPoint, endPoint));
			}
		}
		return pathGeometry;
	}

	public static void AddPolylineGeometry(IPolyline polyline, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		List<Point> polylinePointList = polyline.PolylinePointList;
		if (polylinePointList != null && ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			PathGeometry innerGeometry = GetInnerGeometry(null, polylinePointList, ControlToImageRadio);
			Pen pen = new Pen
			{
				Thickness = polyline.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(polyline.Stroke)
			};
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawGeometry(null, pen, innerGeometry);
			}
			tagDrawingVisual.Opacity = polyline.Opacity;
			tagDrawingVisual.Tag = polyline.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	static PolylineShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PolylineShape), new FrameworkPropertyMetadata(typeof(PolylineShape)));
	}
}
