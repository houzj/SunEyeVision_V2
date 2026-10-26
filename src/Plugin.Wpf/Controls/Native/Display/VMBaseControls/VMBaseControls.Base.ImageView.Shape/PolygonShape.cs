using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class PolygonShape : ShapeControlBase
{
	private const int MIN_POINT_COUNT = 3;

	private PolyLineSegment polyLineSegment = new PolyLineSegment();

	private PathFigure figure = new PathFigure
	{
		IsClosed = true
	};

	public static ShapeControlBase GetPolygonShape(IPolygon polygon, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		List<Point> points = polygon.Points;
		if (points == null || points.Count < 3)
		{
			return null;
		}
		PolygonShape polygonShape = new PolygonShape();
		List<Point> list = points.Select((Point p) => new Point((p.X + 0.0) * ControlToImageRadio, (p.Y + 0.0) * ControlToImageRadio)).ToList();
		polygonShape.figure.StartPoint = list[0];
		polygonShape.polyLineSegment.Points = new PointCollection(list);
		polygonShape.Stroke = DrawShapeHelper.GetBrushByString(polygon.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = polygon.ToolTip;
		toolTip.StaysOpen = true;
		polygonShape.ToolTip = toolTip;
		polygonShape.Opacity = polygon.Opacity;
		polygonShape.Fill = DrawShapeHelper.GetBrushByString(polygon.Fill);
		polygonShape.IsHitTestVisible = !string.IsNullOrEmpty(polygon.ToolTip);
		polygonShape.StrokeThickness = polygon.StrokeThickness / ThicknessScale;
		polygonShape.BaseStrokeThickness = polygon.StrokeThickness;
		return polygonShape;
	}

	public static void AddPolygonGeometry(IPolygon polygon, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return;
		}
		List<Point> points = polygon.Points;
		if (points != null && points.Count >= 3)
		{
			PathGeometry pathGeometry = new PathGeometry();
			PolyLineSegment polyLineSegment = new PolyLineSegment();
			PathFigure pathFigure = new PathFigure
			{
				IsClosed = true
			};
			pathFigure.Segments.Add(polyLineSegment);
			pathGeometry.Figures.Add(pathFigure);
			List<Point> list = points.Select((Point p) => new Point((p.X + 0.0) * ControlToImageRadio, (p.Y + 0.0) * ControlToImageRadio)).ToList();
			pathFigure.StartPoint = list[0];
			polyLineSegment.Points = new PointCollection(list);
			Pen pen = new Pen
			{
				Thickness = polygon.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(polygon.Stroke)
			};
			Brush brushByString = DrawShapeHelper.GetBrushByString(polygon.Fill);
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawGeometry(brushByString, pen, pathGeometry);
			}
			tagDrawingVisual.Opacity = polygon.Opacity;
			tagDrawingVisual.Tag = polygon.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	static PolygonShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PolygonShape), new FrameworkPropertyMetadata(typeof(PolygonShape)));
	}

	public PolygonShape()
	{
		PathGeometry pathGeometry = new PathGeometry();
		figure.Segments.Add(polyLineSegment);
		pathGeometry.Figures.Add(figure);
		geometry.AddGeometry(pathGeometry);
	}
}
