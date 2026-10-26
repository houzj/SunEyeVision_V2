using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class RectShape : ShapeControlBase
{
	public static ShapeControlBase GetRectGeometry(IRectangle Rect, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale)
	{
		if (ImageSourceWidth == 0 || ImageSourceHeight == 0)
		{
			return null;
		}
		RectShape rectShape = new RectShape();
		double num = (Rect.CenterPoint.X - Rect.Width / 2.0 + 0.0) * ControlToImageRadio;
		double num2 = (Rect.CenterPoint.Y - Rect.Height / 2.0 + 0.0) * ControlToImageRadio;
		double num3 = Rect.Width * ControlToImageRadio;
		double num4 = Rect.Height * ControlToImageRadio;
		rectShape.geometry.AddGeometry(new RectangleGeometry
		{
			Rect = new Rect(num, num2, num3, num4)
		});
		rectShape.Stroke = DrawShapeHelper.GetBrushByString(Rect.Stroke);
		ToolTip toolTip = new ToolTip();
		toolTip.Content = Rect.ToolTip;
		toolTip.StaysOpen = true;
		rectShape.Opacity = Rect.Opacity;
		rectShape.ToolTip = toolTip;
		rectShape.StrokeThickness = Rect.StrokeThickness / ThicknessScale;
		rectShape.BaseStrokeThickness = Rect.StrokeThickness;
		TransformGroup renderTransform = new TransformGroup
		{
			Children = new TransformCollection
			{
				new SkewTransform
				{
					AngleX = -1.0 * Rect.SkewAngleX,
					AngleY = -1.0 * Rect.SkewAngleY,
					CenterX = num + num3 / 2.0,
					CenterY = num2 + num4 / 2.0
				},
				new RotateTransform
				{
					Angle = Rect.Angle,
					CenterX = num + num3 / 2.0,
					CenterY = num2 + num4 / 2.0
				}
			}
		};
		rectShape.RenderTransform = renderTransform;
		rectShape.IsHitTestVisible = !string.IsNullOrEmpty(Rect.ToolTip);
		rectShape.Fill = DrawShapeHelper.GetBrushByString(Rect.Fill);
		return rectShape;
	}

	public static void AddRectGeometry(IRectangle Rect, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, double ThicknessScale, ShapeCanvas container)
	{
		if (ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			double num = Rect.CenterPoint.X - Rect.Width / 2.0 + 0.0;
			double num2 = num * ControlToImageRadio;
			double num3 = Rect.CenterPoint.Y - Rect.Height / 2.0 + 0.0;
			double num4 = num3 * ControlToImageRadio;
			double num5 = Rect.Width * ControlToImageRadio;
			double num6 = Rect.Height * ControlToImageRadio;
			TransformGroup transform = new TransformGroup
			{
				Children = new TransformCollection
				{
					new SkewTransform
					{
						AngleX = -1.0 * Rect.SkewAngleX,
						AngleY = -1.0 * Rect.SkewAngleY,
						CenterX = num2 + num5 / 2.0,
						CenterY = num4 + num6 / 2.0
					},
					new RotateTransform
					{
						Angle = Rect.Angle,
						CenterX = num2 + num5 / 2.0,
						CenterY = num4 + num6 / 2.0
					}
				}
			};
			Pen pen = new Pen
			{
				Thickness = Rect.StrokeThickness / ThicknessScale,
				Brush = DrawShapeHelper.GetBrushByString(Rect.Stroke)
			};
			Brush brushByString = DrawShapeHelper.GetBrushByString(Rect.Fill);
			TagDrawingVisual tagDrawingVisual = new TagDrawingVisual();
			using (DrawingContext drawingContext = tagDrawingVisual.RenderOpen())
			{
				drawingContext.DrawRectangle(brushByString, pen, new Rect(new Point(num2, num4), new Size(num5, num6)));
			}
			tagDrawingVisual.Transform = transform;
			tagDrawingVisual.Opacity = Rect.Opacity;
			tagDrawingVisual.Tag = Rect.ToolTip;
			container.AddVisual(tagDrawingVisual, "rect");
			if (Rect is IIndex { Index: >0 } index)
			{
				TextShape.AddTextBlock(Rect, index.Index.ToString(), new Point(num + 2.0, num3 + 2.0), 24, 0.5, ImageSourceWidth, ImageSourceHeight, ControlToImageRadio, container, null);
			}
		}
	}

	static RectShape()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(RectShape), new FrameworkPropertyMetadata(typeof(RectShape)));
	}
}
