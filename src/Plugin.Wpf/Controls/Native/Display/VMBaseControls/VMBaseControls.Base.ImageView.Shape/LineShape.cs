using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class LineShape : ShapeControlBase
{
	public static ShapeControlBase GetLineGeometry(ILine line, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		LineShape lineShape = new LineShape();
		Point startPoint = new Point(0.0, 0.0);
		Point endPoint = new Point(0.0, 0.0);
		bool flag = false;
		if (line.IsThroughLine)
		{
			flag = CalBoderPoint(new Point(line.StartPoint.X, line.StartPoint.Y), new Point(line.EndPoint.X, line.EndPoint.Y), ImageSourceWidth, ImageSourceHeight, out var borderPoint, out var borderPoint2);
			if (flag)
			{
				startPoint.X = (borderPoint.X + 0.0) * ControlToImageRadio;
				startPoint.Y = (borderPoint.Y + 0.0) * ControlToImageRadio;
				endPoint.X = (borderPoint2.X + 0.0) * ControlToImageRadio;
				endPoint.Y = (borderPoint2.Y + 0.0) * ControlToImageRadio;
			}
		}
		if (!flag)
		{
			startPoint.X = (line.StartPoint.X + 0.0) * ControlToImageRadio;
			startPoint.Y = (line.StartPoint.Y + 0.0) * ControlToImageRadio;
			endPoint.X = (line.EndPoint.X + 0.0) * ControlToImageRadio;
			endPoint.Y = (line.EndPoint.Y + 0.0) * ControlToImageRadio;
		}
		lineShape.geometry.AddGeometry(new LineGeometry
		{
			StartPoint = startPoint,
			EndPoint = endPoint
		});
		lineShape.Stroke = DrawShapeHelper.GetBrushByString(line.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = line.ToolTip;
		toolTip.StaysOpen = true;
		lineShape.ToolTip = toolTip;
		lineShape.Opacity = line.Opacity;
		lineShape.Fill = DrawShapeHelper.GetBrushByString(line.Fill);
		lineShape.IsHitTestVisible = !string.IsNullOrEmpty(line.ToolTip);
		lineShape.StrokeThickness = line.StrokeThickness / ThicknessScale;
		lineShape.BaseStrokeThickness = line.StrokeThickness;
		return lineShape;
	}

	public static void AddLineGeometry(ILine line, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container, double[,] matrix)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return;
		}
		double num = 1.0;
		if (ThicknessScale != 0.0)
		{
			num = ThicknessScale;
		}
		Point point = line.StartPoint;
		Point point2 = line.EndPoint;
		Point point3 = new Point(0.0, 0.0);
		Point point4 = new Point(0.0, 0.0);
		if (line is IMatrixEnable { MatrixConvertEnable: not false })
		{
			point = DrawShapeHelper.GetPixelPiont(line.StartPoint, matrix);
			point2 = DrawShapeHelper.GetPixelPiont(line.EndPoint, matrix);
		}
		bool flag = false;
		if (line.IsThroughLine)
		{
			flag = CalBoderPoint(new Point(point.X, point.Y), new Point(point2.X, point2.Y), ImageSourceWidth, ImageSourceHeight, out var borderPoint, out var borderPoint2);
			if (flag)
			{
				point3.X = (borderPoint.X + 0.0) * ControlToImageRadio;
				point3.Y = (borderPoint.Y + 0.0) * ControlToImageRadio;
				point4.X = (borderPoint2.X + 0.0) * ControlToImageRadio;
				point4.Y = (borderPoint2.Y + 0.0) * ControlToImageRadio;
			}
		}
		if (!flag)
		{
			point3.X = (point.X + 0.0) * ControlToImageRadio;
			point3.Y = (point.Y + 0.0) * ControlToImageRadio;
			point4.X = (point2.X + 0.0) * ControlToImageRadio;
			point4.Y = (point2.Y + 0.0) * ControlToImageRadio;
		}
		Pen pen = new Pen
		{
			Thickness = line.StrokeThickness / ThicknessScale,
			Brush = DrawShapeHelper.GetBrushByString(line.Stroke)
		};
		TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
		using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
		{
			drawingContext.DrawLine(pen, point3, point4);
		}
		tagDrawingVisual.Opacity = line.Opacity;
		tagDrawingVisual.Tag = line.ToolTip;
		container.AddVisual(tagDrawingVisual, "rect");
	}

	private static bool CalBoderPoint(Point pt1, Point pt2, int Width, int Height, out Point borderPoint1, out Point borderPoint2)
	{
		borderPoint1 = default;
		borderPoint2 = default;
		if (pt1.X == pt2.X && pt1.Y == pt2.Y)
		{
			return false;
		}
		if (pt1.X == pt2.X)
		{
			borderPoint1.X = pt1.X;
			borderPoint1.Y = 0.0;
			borderPoint2.X = pt1.X;
			borderPoint2.Y = Height;
		}
		else if (pt1.Y == pt2.Y)
		{
			borderPoint1.X = 0.0;
			borderPoint1.Y = pt1.Y;
			borderPoint2.X = Width;
			borderPoint2.Y = pt1.Y;
		}
		else
		{
			Point[] array = new Point[4];
			int[] array2 = new int[4] { 1, 1, 1, 1 };
			double num = 0.0;
			double num2 = 0.0;
			num = (pt1.Y - pt2.Y) / (pt1.X - pt2.X);
			num2 = pt2.Y - num * pt2.X;
			array[0].X = 0.0;
			array[0].Y = num2;
			if (array[0].Y < 0.0 || array[0].Y > (double)Height)
			{
				array2[0] = 0;
			}
			array[1].X = Width;
			array[1].Y = num * array[1].X + num2;
			if (array[1].Y < 0.0 || array[1].Y > (double)Height)
			{
				array2[1] = 0;
			}
			array[2].Y = 0.0;
			array[2].X = (array[2].Y - num2) / num;
			if (array[2].X < 0.0 || array[2].X > (double)Width)
			{
				array2[2] = 0;
			}
			array[3].Y = Height;
			array[3].X = (array[3].Y - num2) / num;
			if (array[3].X < 0.0 || array[3].X > (double)Width)
			{
				array2[3] = 0;
			}
			int num3 = 0;
			for (int i = 0; i < 4; i++)
			{
				if (array2[i] == 1)
				{
					switch (num3)
					{
					case 0:
						borderPoint1.X = array[i].X;
						borderPoint1.Y = array[i].Y;
						num3++;
						break;
					case 1:
						borderPoint2.X = array[i].X;
						borderPoint2.Y = array[i].Y;
						break;
					}
				}
			}
		}
		return true;
	}

	static LineShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(LineShape), new FrameworkPropertyMetadata(typeof(LineShape)));
	}
}
