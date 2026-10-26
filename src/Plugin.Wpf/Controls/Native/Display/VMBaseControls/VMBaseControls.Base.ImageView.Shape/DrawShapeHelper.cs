using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public static class DrawShapeHelper
{
	private struct MultiText : IMultiText, IShape, IShapeBase
	{
		public List<IText> Texts { get; set; }

		public double Opacity { get; set; }

		public string Stroke { get; set; }

		public string Fill { get; set; }

		public double StrokeThickness { get; set; }

		public string ToolTip { get; set; }
	}

	internal const int LowQualityLimit = 1000;

	private static BrushConverter brushConverter = new BrushConverter();

	public static void DrawShapes(List<object> shapes, IList<object> itemsource, ImageViewBox imageView, bool isLowQuality = false)
	{
		try
		{
			if (shapes == null || shapes.Count < 1 || itemsource == null)
			{
				return;
			}
			Type type = shapes.FirstOrDefault().GetType();
			Type type2 = type.GetInterfaces().SingleOrDefault((Type p) => typeof(IShape).IsAssignableFrom(p) && p != typeof(IShape));
			if (type2 == null)
			{
				type2 = type.GetInterfaces().SingleOrDefault((Type p) => typeof(IShapeBase).IsAssignableFrom(p) && p != typeof(IShapeBase) && p != typeof(IShape));
				if (type2 == null)
				{
					return;
				}
			}
			if (type2 == typeof(IText))
			{
				MultiText multiText = (MultiText)PretreatTexts(shapes);
				itemsource.Add(multiText);
				return;
			}
			if (type2 == typeof(IOutlinePoint))
			{
				foreach (object shape in shapes)
				{
					IOutlinePoint item = shape as IOutlinePoint;
					if (itemsource.FirstOrDefault((object p) => p is IMultiPoint) is IMultiPoint multiPoint)
					{
						multiPoint.OutlinePoints.Add(item);
					}
				}
				return;
			}
			if (type2 == typeof(IMicroPoint))
			{
				int count = shapes.Count;
				for (int num = 0; num < count; num++)
				{
					IMicroPoint item2 = shapes[num] as IMicroPoint;
					if (itemsource.FirstOrDefault((object p) => p is IMultiPoint) is IMultiPoint multiPoint2)
					{
						multiPoint2.MicroPoints.Add(item2);
					}
				}
				return;
			}
			if (((type2 == typeof(IRectangle)) & isLowQuality) && shapes.Count > 1000)
			{
				int num2 = Math.Min(shapes.Count, GlobalRenderVariable.ShapeRenderingMaxNumber);
				for (int num3 = 0; num3 < num2; num3++)
				{
					IRectangle item3 = shapes[num3] as IRectangle;
					if (itemsource.FirstOrDefault((object p) => p is IMultiPoint) is IMultiPoint multiPoint3)
					{
						multiPoint3.Rectangles.Add(item3);
					}
				}
				return;
			}
			if (type2 == typeof(ICross) || type2 == typeof(IAngleShape))
			{
				foreach (object shape2 in shapes)
				{
					itemsource.Add(shape2);
				}
				return;
			}
			itemsource.Add(new Tuple<string, object>(type2.Name, shapes));
		}
		catch (Exception)
		{
			throw;
		}
	}

	public static object PretreatTexts(List<object> texts)
	{
		MultiText multiText = new MultiText
		{
			Texts = new List<IText>()
		};
		foreach (object text2 in texts)
		{
			IText text = text2 as IText;
			multiText.Texts.Add(text);
			multiText.Opacity = text.Opacity;
		}
		return multiText;
	}

	public static int XmlColorToInt(string xmlString)
	{
		if (xmlString != "")
		{
			return ColorTranslator.FromHtml(xmlString).ToArgb();
		}
		return 0;
	}

	public static System.Windows.Media.Brush GetBrushByString(string color)
	{
		if (!string.IsNullOrWhiteSpace(color))
		{
			try
			{
				return brushConverter.ConvertFromString(color) as System.Windows.Media.Brush;
			}
			catch
			{
			}
		}
		return null;
	}

	public static System.Windows.Point GetPixelPiont(System.Windows.Point worldPoint, double[,] matrix)
	{
		if (matrix == null)
		{
			return worldPoint;
		}
		System.Windows.Point result = new System.Windows.Point(worldPoint.X, worldPoint.Y);
		int length = matrix.GetLength(0);
		int length2 = matrix.GetLength(1);
		if (length == 3 && length2 == 3)
		{
			double num = matrix[2, 0] * worldPoint.X + matrix[2, 1] * worldPoint.Y + matrix[2, 2];
			double x = (matrix[0, 0] * worldPoint.X + matrix[0, 1] * worldPoint.Y + matrix[0, 2]) / num;
			double y = (matrix[1, 0] * worldPoint.X + matrix[1, 1] * worldPoint.Y + matrix[1, 2]) / num;
			result = new System.Windows.Point(x, y);
		}
		return result;
	}

	public static FormattedText MeasureText(string str, System.Windows.Media.FontFamily fontFamily, System.Windows.FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, double FontSize)
	{
		return new FormattedText(str, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(fontFamily, fontStyle, fontWeight, fontStretch), FontSize, System.Windows.Media.Brushes.Black);
	}

	public static System.Windows.Size GetTextDisplayWidth(string str, System.Windows.Media.FontFamily fontFamily, System.Windows.FontStyle fontStyle, FontWeight fontWeight, FontStretch fontStretch, double FontSize)
	{
		FormattedText formattedText = MeasureText(str, fontFamily, fontStyle, fontWeight, fontStretch, FontSize);
		return new System.Windows.Size(formattedText.Width, formattedText.Height);
	}
}
