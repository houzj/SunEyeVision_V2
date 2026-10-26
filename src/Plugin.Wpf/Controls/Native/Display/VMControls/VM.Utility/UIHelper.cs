using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

public static class UIHelper
{
	public static T GetParent<T>(this DependencyObject source) where T : UIElement
	{
		DependencyObject parent = VisualTreeHelper.GetParent(source);
		if (parent == null)
		{
			return null;
		}
		while (parent != null && !(parent is T))
		{
			parent = parent.GetParent<T>();
		}
		return parent as T;
	}

	public static void DrawArc(this DrawingContext dc, Pen pen, Brush brush, Rect rect, double startDegrees, double sweepDegrees)
	{
		GeometryDrawing geometryDrawing = CreateArcDrawing(rect, startDegrees, sweepDegrees);
		dc.DrawGeometry(brush, pen, geometryDrawing.Geometry);
	}

	private static GeometryDrawing CreateArcDrawing(Rect rect, double startDegrees, double sweepDegrees)
	{
		double num = startDegrees * Math.PI / 180.0;
		double num2 = sweepDegrees * Math.PI / 180.0;
		double num3 = rect.Width / 2.0;
		double num4 = rect.Height / 2.0;
		double x = rect.X + num3 + Math.Cos(num) * num3;
		double y = rect.Y + num4 + Math.Sin(num) * num4;
		double x2 = rect.X + num3 + Math.Cos(num + num2) * num3;
		double y2 = rect.Y + num4 + Math.Sin(num + num2) * num4;
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			bool isLargeArc = Math.Abs(sweepDegrees) > 180.0;
			SweepDirection sweepDirection = ((!(sweepDegrees < 0.0)) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise);
			streamGeometryContext.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
			streamGeometryContext.ArcTo(new Point(x2, y2), new Size(num3, num4), 0.0, isLargeArc, sweepDirection, isStroked: true, isSmoothJoin: false);
		}
		return new GeometryDrawing
		{
			Geometry = streamGeometry
		};
	}

	public static void DrawArc(this DrawingContext dc, Pen pen, Brush brush, Point centerPoint, double radius, double startDegrees, double sweepDegrees)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			bool isLargeArc = Math.Abs(sweepDegrees) > 180.0;
			SweepDirection sweepDirection = ((!(sweepDegrees < 0.0)) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise);
			streamGeometryContext.BeginFigure(centerPoint.GetEndPoint(startDegrees, radius), isFilled: false, isClosed: false);
			streamGeometryContext.ArcTo(centerPoint.GetEndPoint(startDegrees + sweepDegrees, radius), new Size(radius, radius), 0.0, isLargeArc, sweepDirection, isStroked: true, isSmoothJoin: false);
		}
		dc.DrawGeometry(brush, pen, streamGeometry);
	}

	public static void DrawArcLine(this DrawingContext dc, Pen pen, Brush brush, Point centerPoint, double radius, double startDegrees, double sweepDegrees)
	{
		StreamGeometry streamGeometry = new StreamGeometry();
		using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
		{
			bool isLargeArc = Math.Abs(sweepDegrees) > 180.0;
			SweepDirection sweepDirection = ((!(sweepDegrees < 0.0)) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise);
			streamGeometryContext.BeginFigure(centerPoint.GetEndPoint(startDegrees, radius), isFilled: false, isClosed: false);
			streamGeometryContext.ArcTo(centerPoint.GetEndPoint(startDegrees + sweepDegrees, radius), new Size(radius, radius), 0.0, isLargeArc, sweepDirection, isStroked: true, isSmoothJoin: false);
		}
		dc.DrawGeometry(brush, pen, streamGeometry);
	}

	public static Point[] RectToPoints(this Rect rect)
	{
		return new Point[4]
		{
			new Point
			{
				X = rect.Left,
				Y = rect.Top
			},
			new Point
			{
				X = rect.Right,
				Y = rect.Top
			},
			new Point
			{
				X = rect.Right,
				Y = rect.Bottom
			},
			new Point
			{
				X = rect.Left,
				Y = rect.Bottom
			}
		};
	}
}
