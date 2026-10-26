using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class CoordinateShape : ShapeControlBase
{
	private const double RATIO = 0.05;

	private const int MIN_POINT_COUNT = 3;

	public static ShapeControlBase GetCoordinate(ICoordinateShape coordinate, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		CoordinateShape coordinateShape = new CoordinateShape();
		Point point = new Point((coordinate.CenterPoint.X + 0.0) * ControlToImageRadio, (coordinate.CenterPoint.Y + 0.0) * ControlToImageRadio);
		Vector vector = new Vector(coordinate.VectorX.X, coordinate.VectorX.Y);
		vector.Normalize();
		if (double.IsNaN(vector.X) || double.IsNaN(vector.Y))
		{
			return null;
		}
		double num = 0.05 * (double)ImageSourceWidth * ControlToImageRadio;
		Point point2 = point + vector * num;
		vector = new Vector(coordinate.VectorY.X, coordinate.VectorY.Y);
		vector.Normalize();
		if (double.IsNaN(vector.X) || double.IsNaN(vector.Y))
		{
			return null;
		}
		Point point3 = point + vector * num;
		coordinateShape.geometry.AddGeometry(new LineGeometry(point, point2));
		coordinateShape.geometry.AddGeometry(new LineGeometry(point, point3));
		CreateArrow(coordinateShape.geometry, point, point2, ControlToImageRadio);
		CreateArrow(coordinateShape.geometry, point, point3, ControlToImageRadio);
		Geometry geometry = CreateText("X", point2);
		Geometry geometry2 = CreateText("Y", point3);
		coordinateShape.geometry.AddGeometry(geometry);
		coordinateShape.geometry.AddGeometry(geometry2);
		coordinateShape.Stroke = DrawShapeHelper.GetBrushByString(coordinate.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = coordinate.ToolTip;
		toolTip.StaysOpen = true;
		coordinateShape.ToolTip = toolTip;
		coordinateShape.Opacity = coordinate.Opacity;
		coordinateShape.Fill = DrawShapeHelper.GetBrushByString(coordinate.Stroke);
		coordinateShape.IsHitTestVisible = !string.IsNullOrEmpty(coordinate.ToolTip);
		coordinateShape.StrokeThickness = coordinate.StrokeThickness / ThicknessScale;
		coordinateShape.BaseStrokeThickness = coordinate.StrokeThickness;
		return coordinateShape;
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

	private static Geometry CreateText(string text, Point position)
	{
		FontStyle normal = FontStyles.Normal;
		FontWeight bold = FontWeights.Bold;
		int num = 12;
		FormattedText formattedText = new FormattedText(text, CultureInfo.GetCultureInfo("en-us"), FlowDirection.LeftToRight, new Typeface(new FontFamily("Times New Roman"), normal, bold, FontStretches.Normal), num, Brushes.Red);
		return formattedText.BuildGeometry(position);
	}

	private static PathGeometry GetInnerGeometry(ICoordinateShape coordinate, int ImageSourceWidth, double ControlToImageRadio)
	{
		PathGeometry pathGeometry = new PathGeometry();
		Point point = new Point((coordinate.CenterPoint.X + 0.0) * ControlToImageRadio, (coordinate.CenterPoint.Y + 0.0) * ControlToImageRadio);
		Vector vector = new Vector(coordinate.VectorX.X, coordinate.VectorX.Y);
		vector.Normalize();
		if (double.IsNaN(vector.X) || double.IsNaN(vector.Y))
		{
			return null;
		}
		double num = 0.05 * (double)ImageSourceWidth * ControlToImageRadio;
		Point point2 = point + vector * num;
		vector = new Vector(coordinate.VectorY.X, coordinate.VectorY.Y);
		vector.Normalize();
		if (double.IsNaN(vector.X) || double.IsNaN(vector.Y))
		{
			return null;
		}
		Point point3 = point + vector * num;
		pathGeometry.AddGeometry(new LineGeometry(point, point2));
		pathGeometry.AddGeometry(new LineGeometry(point, point3));
		CreateArrow(pathGeometry, point, point2, ControlToImageRadio);
		CreateArrow(pathGeometry, point, point3, ControlToImageRadio);
		Geometry geometry = CreateText("X", point2);
		Geometry geometry2 = CreateText("Y", point3);
		pathGeometry.AddGeometry(geometry);
		pathGeometry.AddGeometry(geometry2);
		return pathGeometry;
	}

	public static void AddCoordinateGeometry(ICoordinateShape coordinate, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			PathGeometry innerGeometry = GetInnerGeometry(coordinate, ImageSourceWidth, ControlToImageRadio);
			Pen pen = new Pen
			{
				Thickness = coordinate.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(coordinate.Stroke)
			};
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawGeometry(null, pen, innerGeometry);
			}
			tagDrawingVisual.Opacity = coordinate.Opacity;
			tagDrawingVisual.Tag = coordinate.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
		}
	}

	static CoordinateShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(CoordinateShape), new FrameworkPropertyMetadata(typeof(CoordinateShape)));
	}
}
