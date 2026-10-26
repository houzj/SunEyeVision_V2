using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class ArrowPathShape : ShapeControlBase
{
	public static ShapeControlBase GetSegmentData(IArrowPath arrowPath, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		ArrowPathShape arrowPathShape = new ArrowPathShape();
		List<Point> points = arrowPath.Points;
		if (points == null || ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		GetInnerGeometry(arrowPathShape.geometry, points, ControlToImageRadio);
		arrowPathShape.Stroke = DrawShapeHelper.GetBrushByString(arrowPath.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = arrowPath.ToolTip;
		toolTip.StaysOpen = true;
		arrowPathShape.ToolTip = toolTip;
		arrowPathShape.Opacity = arrowPath.Opacity;
		arrowPathShape.Fill = DrawShapeHelper.GetBrushByString(arrowPath.Fill);
		arrowPathShape.IsHitTestVisible = !string.IsNullOrEmpty(arrowPath.ToolTip);
		arrowPathShape.StrokeThickness = arrowPath.StrokeThickness / ThicknessScale;
		arrowPathShape.BaseStrokeThickness = arrowPath.StrokeThickness;
		return arrowPathShape;
	}

	private static void CreateArrow(PathGeometry geometry, Point startPoint, Point endPoint, double ControlToImageRadio)
	{
		double num = 10.0 * ControlToImageRadio;
		double num2 = Math.Atan2(startPoint.Y - endPoint.Y, startPoint.X - endPoint.X);
		num2 = num2 * 180.0 / Math.PI;
		RotateTransform rotateTransform = new RotateTransform
		{
			CenterX = endPoint.X,
			CenterY = endPoint.Y,
			Angle = num2
		};
		Point point = new Point(endPoint.X + num, endPoint.Y);
		point = rotateTransform.Transform(point);
		rotateTransform = new RotateTransform
		{
			CenterX = endPoint.X,
			CenterY = endPoint.Y,
			Angle = 45.0
		};
		Point endPoint2 = rotateTransform.Transform(point);
		geometry.AddGeometry(new LineGeometry(endPoint, endPoint2));
		rotateTransform = new RotateTransform
		{
			CenterX = endPoint.X,
			CenterY = endPoint.Y,
			Angle = -45.0
		};
		Point endPoint3 = rotateTransform.Transform(point);
		geometry.AddGeometry(new LineGeometry(endPoint, endPoint3));
	}

	private static PathGeometry GetInnerGeometry(PathGeometry geometry, List<Point> points, double ControlToImageRadio)
	{
		PathGeometry pathGeometry = geometry ?? new PathGeometry();
		float num = 0.8f;
		int count = points.Count;
		for (int i = 0; i < count; i++)
		{
			Point point = points[i];
			double x = point.X;
			double y = point.Y;
			if (i + 1 < count)
			{
				Point point2 = points[i + 1];
				double num2 = point2.X - x;
				double num3 = point2.Y - y;
				double x2 = (x + (double)(1f - num) * num2 + 0.0) * ControlToImageRadio;
				double y2 = (y + (double)(1f - num) * num3 + 0.0) * ControlToImageRadio;
				double x3 = (x + num2 * (double)num + 0.0) * ControlToImageRadio;
				double y3 = (y + (double)num * num3 + 0.0) * ControlToImageRadio;
				Point startPoint = new Point(x2, y2);
				Point endPoint = new Point(x3, y3);
				pathGeometry.AddGeometry(new LineGeometry(startPoint, endPoint));
				CreateArrow(pathGeometry, startPoint, endPoint, ControlToImageRadio);
			}
		}
		return pathGeometry;
	}

	public static void AddArrowPathGeometry(IArrowPath arrowPath, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		List<Point> points = arrowPath.Points;
		if (points != null && ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			PathGeometry innerGeometry = GetInnerGeometry(null, points, ControlToImageRadio);
			Pen pen = new Pen
			{
				Thickness = arrowPath.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(arrowPath.Stroke)
			};
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawGeometry(null, pen, innerGeometry);
			}
			tagDrawingVisual.Opacity = arrowPath.Opacity;
			tagDrawingVisual.Tag = arrowPath.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	static ArrowPathShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ArrowPathShape), new FrameworkPropertyMetadata(typeof(ArrowPathShape)));
	}
}
