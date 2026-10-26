using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class PointShape : ShapeControlBase
{
	private const double WIDTH_BASE = 3.0;

	public Point CenterPoint { get; set; }

	public static System.Windows.Shapes.Shape GetPointGeometry(IPoint point, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		PointShape pointShape = new PointShape();
		double num = 1.0;
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		if (ThicknessScale != 0.0)
		{
			num = ThicknessScale;
		}
		pointShape.geometry.AddGeometry(new LineGeometry());
		pointShape.geometry.AddGeometry(new LineGeometry());
		pointShape.Stroke = DrawShapeHelper.GetBrushByString(point.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = point.ToolTip;
		toolTip.StaysOpen = true;
		pointShape.ToolTip = toolTip;
		pointShape.Opacity = point.Opacity;
		pointShape.Fill = DrawShapeHelper.GetBrushByString(point.Fill);
		pointShape.IsHitTestVisible = !string.IsNullOrEmpty(point.ToolTip);
		pointShape.StrokeThickness = point.StrokeThickness / num;
		pointShape.BaseStrokeThickness = point.StrokeThickness;
		pointShape.ControlToImageRadio = ControlToImageRadio;
		pointShape.ImageSourceWidth = ImageSourceWidth;
		pointShape.ImageSourceHeight = ImageSourceHeight;
		pointShape.CenterPoint = point.Point;
		if (point is IIndex { Index: >0 } index)
		{
			TextShape.AddTextBlock(point, index.Index.ToString(), point.Point, 10, 0.5, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, container, null);
		}
		return pointShape;
	}

	public static void AddPointGeometry(IPoint point, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container, double[,] matrix)
	{
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			double num = 1.0;
			if (ThicknessScale != 0.0)
			{
				num = ThicknessScale;
			}
			Point point2 = point.Point;
			if (point is IMatrixEnable { MatrixConvertEnable: not false })
			{
				point2 = DrawShapeHelper.GetPixelPiont(point.Point, matrix);
			}
			Point point3 = new Point((point2.X + 0.0) * ControlToImageRadio, (point2.Y + 0.0) * ControlToImageRadio - point.StrokeThickness * 3.0 / num);
			Point point4 = new Point((point2.X + 0.0) * ControlToImageRadio, (point2.Y + 0.0) * ControlToImageRadio + point.StrokeThickness * 3.0 / num);
			Point point5 = new Point((point2.X + 0.0) * ControlToImageRadio - point.StrokeThickness * 3.0 / num, (point2.Y + 0.0) * ControlToImageRadio);
			Point point6 = new Point((point2.X + 0.0) * ControlToImageRadio + point.StrokeThickness * 3.0 / num, (point2.Y + 0.0) * ControlToImageRadio);
			Pen pen = new Pen
			{
				Thickness = point.StrokeThickness / num,
				Brush = DrawShapeHelper.GetBrushByString(point.Stroke)
			};
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawLine(pen, point3, point4);
				drawingContext.DrawLine(pen, point5, point6);
			}
			tagDrawingVisual.Opacity = point.Opacity;
			tagDrawingVisual.Tag = point.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	public static void AddPointsGeometry(List<object> points, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container, double[,] matrix)
	{
		if (points == null || points.Count == 0 || ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return;
		}
		DrawingVisual drawingVisual = new DrawingVisual();
		double num = 1.0;
		if (ThicknessScale != 0.0)
		{
			num = ThicknessScale;
		}
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			foreach (IPoint point7 in points)
			{
				Point point2 = point7.Point;
				if (point7 is IMatrixEnable { MatrixConvertEnable: not false })
				{
					point2 = DrawShapeHelper.GetPixelPiont(point7.Point, matrix);
				}
				Point point3 = new Point((point2.X + 0.0) * ControlToImageRadio, (point2.Y + 0.0) * ControlToImageRadio - point7.StrokeThickness * 3.0 / num);
				Point point4 = new Point((point2.X + 0.0) * ControlToImageRadio, (point2.Y + 0.0) * ControlToImageRadio + point7.StrokeThickness * 3.0 / num);
				Point point5 = new Point((point2.X + 0.0) * ControlToImageRadio - point7.StrokeThickness * 3.0 / num, (point2.Y + 0.0) * ControlToImageRadio);
				Point point6 = new Point((point2.X + 0.0) * ControlToImageRadio + point7.StrokeThickness * 3.0 / num, (point2.Y + 0.0) * ControlToImageRadio);
				Pen pen = new Pen
				{
					Thickness = point7.StrokeThickness / num,
					Brush = DrawShapeHelper.GetBrushByString(point7.Stroke)
				};
				drawingContext.DrawLine(pen, point3, point4);
				drawingContext.DrawLine(pen, point5, point6);
			}
		}
		drawingVisual.Opacity = (points[0] as IPoint)?.Opacity ?? 1.0;
		container.AddVisual(drawingVisual, "rect");
	}

	protected override void ChangeThickness()
	{
		base.ChangeThickness();
		SetLineGeometry();
	}

	private void SetLineGeometry()
	{
		double num = 1.0;
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			if (ThicknessScale != 0.0)
			{
				num = ThicknessScale;
			}
			geometry.Clear();
			geometry.AddGeometry(new LineGeometry
			{
				StartPoint = new Point((CenterPoint.X + 0.0) * ControlToImageRadio, (CenterPoint.Y + 0.0) * ControlToImageRadio - BaseStrokeThickness * 3.0 / num),
				EndPoint = new Point((CenterPoint.X + 0.0) * ControlToImageRadio, (CenterPoint.Y + 0.0) * ControlToImageRadio + BaseStrokeThickness * 3.0 / num)
			});
			geometry.AddGeometry(new LineGeometry
			{
				StartPoint = new Point((CenterPoint.X + 0.0) * ControlToImageRadio - BaseStrokeThickness * 3.0 / num, (CenterPoint.Y + 0.0) * ControlToImageRadio),
				EndPoint = new Point((CenterPoint.X + 0.0) * ControlToImageRadio + BaseStrokeThickness * 3.0 / num, (CenterPoint.Y + 0.0) * ControlToImageRadio)
			});
		}
	}

	static PointShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PointShape), new FrameworkPropertyMetadata(typeof(PointShape)));
	}
}
