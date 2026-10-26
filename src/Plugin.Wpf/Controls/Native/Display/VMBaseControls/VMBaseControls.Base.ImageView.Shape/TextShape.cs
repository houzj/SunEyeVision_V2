using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

[ShapeDisplayForInterfaceType(typeof(IMultiText))]
public class TextShape
{
	private static readonly double TEXT_SHAPE_SPACE = 0.1;

	public static int AddMultiText(IMultiText multiText, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, ShapeCanvas container, ImageViewBox imageView)
	{
		int num = 0;
		if (multiText.Texts != null && ImageSourceWidth != 0 && ImageSourceHeight != 0)
		{
			double num2 = 1.0;
			if (imageView != null)
			{
				double num3 = Math.Min(imageView.ActualWidth, imageView.GetImageViewWidth());
				double num4 = Math.Min(imageView.ActualHeight, imageView.GetImageViewHeight());
				if (num3 > 0.0 && num4 > 0.0)
				{
					num2 = num4 / num3;
				}
			}
			List<IGrouping<Point, IText>> list = (from p in multiText.Texts
				group p by p.Position).ToList();
			foreach (IGrouping<Point, IText> item in list)
			{
				DrawingVisual drawingVisual = new DrawingVisual();
				double num5 = 0.0;
				using (DrawingContext drawingContext = drawingVisual.RenderOpen())
				{
					foreach (IText item2 in item)
					{
						IText text = item2;
						if (text.Position.X > (double)(ImageSourceWidth * 3) || text.Position.Y + num5 > (double)(ImageSourceHeight * 3))
						{
							break;
						}
						int num6 = text.FontSize;
						string content = text.Content;
						if (num6 <= 0 || num6 > 1000)
						{
							num6 = 10;
						}
						double fontSize = num6;
						if (num2 > 1.0)
						{
							fontSize = (double)num6 * num2;
						}
						if (text is ISize { Width: not 0.0, Height: not 0.0 } size)
						{
							fontSize = Math.Min(CalculateMaxWidthFontSize(size.Width * ControlToImageRadio, num6, content, SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal), CalculateMaxHeightFontSize(size.Height * ControlToImageRadio, num6, content, SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal));
						}
						FormattedText formattedText = DrawShapeHelper.MeasureText(content, SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal, fontSize);
						formattedText.SetForegroundBrush(DrawShapeHelper.GetBrushByString(text.Stroke));
						formattedText.MaxTextWidth = (double)ImageSourceWidth * ControlToImageRadio;
						drawingContext.DrawText(formattedText, new Point(text.Position.X * ControlToImageRadio, (text.Position.Y + num5) * ControlToImageRadio));
						num5 = formattedText.Height / ControlToImageRadio + num5 + 0.5;
						num++;
					}
				}
				drawingVisual.Opacity = multiText.Opacity;
				container.AddVisual(drawingVisual, "text");
			}
		}
		return num;
	}

	internal static double AddTextBlock(IShapeBase text, string content, Point pos, int fontSize, double addPos, int ImageSourceWidth, int ImageSourceHeight, double ControlToImageRadio, ShapeCanvas container, ImageViewBox imageView)
	{
		if (fontSize <= 0 || fontSize > 1000)
		{
			fontSize = 10;
		}
		double fontSize2 = fontSize;
		if (imageView != null)
		{
			double num = Math.Min(imageView.ActualWidth, imageView.GetImageViewWidth());
			double num2 = Math.Min(imageView.ActualHeight, imageView.GetImageViewHeight());
			if (num > 0.0 && num2 > 0.0)
			{
				double num3 = num2 / num;
				if (num3 > 1.0)
				{
					fontSize2 = (double)fontSize * num3;
				}
			}
		}
		if (text is ISize { Width: not 0.0, Height: not 0.0 } size)
		{
			fontSize2 = Math.Min(CalculateMaxWidthFontSize(size.Width * ControlToImageRadio, fontSize, content, SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal), CalculateMaxHeightFontSize(size.Height * ControlToImageRadio, fontSize, content, SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal));
		}
		DrawingVisual drawingVisual = new DrawingVisual();
		FormattedText formattedText = DrawShapeHelper.MeasureText(content, SystemFonts.MessageFontFamily, SystemFonts.MessageFontStyle, SystemFonts.MessageFontWeight, FontStretches.Normal, fontSize2);
		formattedText.SetForegroundBrush(DrawShapeHelper.GetBrushByString(text.Stroke));
		formattedText.MaxTextWidth = (double)ImageSourceWidth * ControlToImageRadio;
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawText(formattedText, new Point(pos.X * ControlToImageRadio, (pos.Y + addPos) * ControlToImageRadio));
		}
		Size size2 = new Size(formattedText.Width, formattedText.Height);
		drawingVisual.Opacity = text.Opacity;
		container.AddVisual(drawingVisual, "text");
		return size2.Height / ControlToImageRadio + addPos + 0.5;
	}

	private static double CalculateMaxWidthFontSize(double width, double endFontSize, string text, FontFamily fontFamily, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch)
	{
		if (double.IsNaN(width))
		{
			return endFontSize;
		}
		if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, endFontSize).WidthIncludingTrailingWhitespace < width)
		{
			return endFontSize;
		}
		double num = 0.0;
		while (endFontSize > 0.0)
		{
			double num2 = (endFontSize + num) / 2.0;
			if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2).WidthIncludingTrailingWhitespace < width && DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2 + TEXT_SHAPE_SPACE).WidthIncludingTrailingWhitespace > width)
			{
				return num2;
			}
			if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2).WidthIncludingTrailingWhitespace < width)
			{
				num = num2 + TEXT_SHAPE_SPACE;
				continue;
			}
			if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2).WidthIncludingTrailingWhitespace > width)
			{
				endFontSize = num2 - TEXT_SHAPE_SPACE;
				continue;
			}
			return num2;
		}
		return endFontSize;
	}

	private static double CalculateMaxHeightFontSize(double height, double endFontSize, string text, FontFamily fontFamily, FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch)
	{
		if (double.IsNaN(height))
		{
			return endFontSize;
		}
		if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, endFontSize).Height < height)
		{
			return endFontSize;
		}
		double num = 0.0;
		while (endFontSize > 0.0)
		{
			double num2 = (endFontSize + num) / 2.0;
			if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2).Height < height && DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2 + TEXT_SHAPE_SPACE).Height > height)
			{
				return num2;
			}
			if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2).Height < height)
			{
				num = num2 + TEXT_SHAPE_SPACE;
				continue;
			}
			if (DrawShapeHelper.MeasureText(text, fontFamily, fontStyle, fontWeight, fontStretch, num2).Height > height)
			{
				endFontSize = num2 - TEXT_SHAPE_SPACE;
				continue;
			}
			return num2;
		}
		return endFontSize;
	}
}
