using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class AnnularSectorShape : ShapeControlBase
{
	public static System.Windows.Shapes.Shape GetAnnularGeometry(IAnnularSector Annular, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (Annular.InnerRadius < 0.0 || Annular.OuterRadius < Annular.InnerRadius || ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		AnnularSectorShape annularSectorShape = new AnnularSectorShape();
		PathGeometry innerGeometry = GetInnerGeometry(Annular, ControlToImageRadio);
		annularSectorShape.geometry.AddGeometry(innerGeometry);
		annularSectorShape.Stroke = DrawShapeHelper.GetBrushByString(Annular.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = Annular.ToolTip;
		toolTip.StaysOpen = true;
		annularSectorShape.ToolTip = toolTip;
		annularSectorShape.Opacity = Annular.Opacity;
		annularSectorShape.Fill = DrawShapeHelper.GetBrushByString(Annular.Fill);
		annularSectorShape.IsHitTestVisible = !string.IsNullOrEmpty(Annular.ToolTip);
		annularSectorShape.StrokeThickness = Annular.StrokeThickness / ThicknessScale;
		annularSectorShape.BaseStrokeThickness = Annular.StrokeThickness;
		return annularSectorShape;
	}

	private static PathGeometry GetInnerGeometry(IAnnularSector Annular, double ControlToImageRadio)
	{
		PathGeometry pathGeometry = new PathGeometry();
		PathFigure pathFigure = new PathFigure();
		RotateTransform rotateTransform = new RotateTransform();
		double num = (Annular.CenterPoint.X + 0.0) * ControlToImageRadio;
		double num2 = (Annular.CenterPoint.Y + 0.0) * ControlToImageRadio;
		double num3 = Annular.InnerRadius * ControlToImageRadio;
		double num4 = Annular.OuterRadius * ControlToImageRadio;
		rotateTransform.CenterX = num;
		rotateTransform.CenterY = num2;
		rotateTransform.Angle = Annular.StartAngle;
		pathFigure.StartPoint = rotateTransform.Transform(new Point(num + num3, num2));
		rotateTransform.Angle = Annular.StartAngle + Annular.AngleRange;
		if (Annular.AngleRange == 360.0)
		{
			rotateTransform.Angle -= 0.01;
		}
		Point point = rotateTransform.Transform(new Point(num + num3, num2));
		pathFigure.Segments.Add(new ArcSegment
		{
			Size = new Size(num3, num3),
			IsLargeArc = (Annular.AngleRange > 180.0),
			SweepDirection = SweepDirection.Clockwise,
			Point = new Point(point.X, point.Y)
		});
		point = rotateTransform.Transform(new Point(num + num4, num2));
		pathFigure.Segments.Add(new LineSegment
		{
			Point = new Point(point.X, point.Y),
			IsStroked = (Annular.AngleRange != 360.0)
		});
		rotateTransform.Angle = Annular.StartAngle;
		point = rotateTransform.Transform(new Point(num + num4, num2));
		pathFigure.Segments.Add(new ArcSegment
		{
			Size = new Size(num4, num4),
			IsLargeArc = (Annular.AngleRange > 180.0),
			Point = new Point(point.X, point.Y)
		});
		pathFigure.Segments.Add(new LineSegment
		{
			Point = new Point(pathFigure.StartPoint.X, pathFigure.StartPoint.Y),
			IsStroked = (Annular.AngleRange != 360.0)
		});
		pathGeometry.Figures.Add(pathFigure);
		return pathGeometry;
	}

	public static void AddAnnularGeometry(IAnnularSector Annular, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		if (!(Annular.InnerRadius < 0.0) && !(Annular.OuterRadius < Annular.InnerRadius) && ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			PathGeometry innerGeometry = GetInnerGeometry(Annular, ControlToImageRadio);
			Pen pen = new Pen
			{
				Thickness = Annular.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(Annular.Stroke)
			};
			Brush brushByString = DrawShapeHelper.GetBrushByString(Annular.Fill);
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawGeometry(brushByString, pen, innerGeometry);
			}
			tagDrawingVisual.Opacity = Annular.Opacity;
			tagDrawingVisual.Tag = Annular.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	static AnnularSectorShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(AnnularSectorShape), new FrameworkPropertyMetadata(typeof(AnnularSectorShape)));
	}
}
