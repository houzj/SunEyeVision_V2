using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class CircleShape : ShapeControlBase
{
	public static ShapeControlBase GetCircleGeometry(ICircle Circle, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		CircleShape circleShape = new CircleShape();
		double num = Circle.Radius * ControlToImageRadio;
		double x = (Circle.CenterPoint.X + 0.0) * ControlToImageRadio;
		double y = (Circle.CenterPoint.Y + 0.0) * ControlToImageRadio;
		circleShape.geometry.AddGeometry(new EllipseGeometry
		{
			RadiusX = num,
			RadiusY = num,
			Center = new Point(x, y)
		});
		circleShape.Stroke = DrawShapeHelper.GetBrushByString(Circle.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = Circle.ToolTip;
		toolTip.StaysOpen = true;
		circleShape.ToolTip = toolTip;
		circleShape.Opacity = Circle.Opacity;
		circleShape.Fill = DrawShapeHelper.GetBrushByString(Circle.Fill);
		circleShape.IsHitTestVisible = !string.IsNullOrEmpty(Circle.ToolTip);
		circleShape.StrokeThickness = Circle.StrokeThickness / ThicknessScale;
		circleShape.BaseStrokeThickness = Circle.StrokeThickness;
		return circleShape;
	}

	public static ShapeControlBase GetEllipseGeometry(IEllipse Ellipse, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		CircleShape circleShape = new CircleShape();
		double radiusX = Ellipse.MajorRadius * ControlToImageRadio;
		double radiusY = Ellipse.MinorRadius * ControlToImageRadio;
		double num = (Ellipse.CenterPoint.X + 0.0) * ControlToImageRadio;
		double num2 = (Ellipse.CenterPoint.Y + 0.0) * ControlToImageRadio;
		circleShape.geometry.AddGeometry(new EllipseGeometry
		{
			RadiusX = radiusX,
			RadiusY = radiusY,
			Center = new Point(num, num2)
		});
		circleShape.Stroke = DrawShapeHelper.GetBrushByString(Ellipse.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = Ellipse.ToolTip;
		toolTip.StaysOpen = true;
		circleShape.ToolTip = toolTip;
		circleShape.Opacity = Ellipse.Opacity;
		circleShape.Fill = DrawShapeHelper.GetBrushByString(Ellipse.Fill);
		circleShape.IsHitTestVisible = !string.IsNullOrEmpty(Ellipse.ToolTip);
		circleShape.StrokeThickness = Ellipse.StrokeThickness / ThicknessScale;
		circleShape.BaseStrokeThickness = Ellipse.StrokeThickness;
		TransformGroup renderTransform = new TransformGroup
		{
			Children = new TransformCollection
			{
				new RotateTransform
				{
					Angle = Ellipse.Angle,
					CenterX = num,
					CenterY = num2
				}
			}
		};
		circleShape.RenderTransform = renderTransform;
		return circleShape;
	}

	public static void AddCircleGeometry(ICircle Circle, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container, double[,] matrix)
	{
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			Point point = Circle.CenterPoint;
			if (Circle is IMatrixEnable { MatrixConvertEnable: not false })
			{
				point = DrawShapeHelper.GetPixelPiont(Circle.CenterPoint, matrix);
			}
			double num = Circle.Radius * ControlToImageRadio;
			double x = (point.X + 0.0) * ControlToImageRadio;
			double y = (point.Y + 0.0) * ControlToImageRadio;
			Pen pen = new Pen
			{
				Thickness = Circle.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(Circle.Stroke)
			};
			Brush brushByString = DrawShapeHelper.GetBrushByString(Circle.Fill);
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawEllipse(brushByString, pen, new Point(x, y), num, num);
			}
			tagDrawingVisual.Opacity = Circle.Opacity;
			tagDrawingVisual.Tag = Circle.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	public static void AddEllipseGeometry(IEllipse ellipse, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			double radiusX = ellipse.MajorRadius * ControlToImageRadio;
			double radiusY = ellipse.MinorRadius * ControlToImageRadio;
			double num = (ellipse.CenterPoint.X + 0.0) * ControlToImageRadio;
			double num2 = (ellipse.CenterPoint.Y + 0.0) * ControlToImageRadio;
			TransformGroup transform = new TransformGroup
			{
				Children = new TransformCollection
				{
					new RotateTransform
					{
						Angle = ellipse.Angle,
						CenterX = num,
						CenterY = num2
					}
				}
			};
			Pen pen = new Pen
			{
				Thickness = ellipse.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(ellipse.Stroke)
			};
			Brush brushByString = DrawShapeHelper.GetBrushByString(ellipse.Fill);
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawEllipse(brushByString, pen, new Point(num, num2), radiusX, radiusY);
			}
			tagDrawingVisual.Transform = transform;
			tagDrawingVisual.Opacity = ellipse.Opacity;
			tagDrawingVisual.Tag = ellipse.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	static CircleShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CircleShape), new FrameworkPropertyMetadata(typeof(CircleShape)));
	}
}
