using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class FouerColorBorder : Border
{
	public static readonly DependencyProperty LeftBorderBrushProperty = DependencyProperty.Register("LeftBorderBrush", typeof(Brush), typeof(FouerColorBorder), new PropertyMetadata(null));

	public static readonly DependencyProperty TopBorderBrushProperty = DependencyProperty.Register("TopBorderBrush", typeof(Brush), typeof(FouerColorBorder), new PropertyMetadata(null));

	public static readonly DependencyProperty RightBorderBrushProperty = DependencyProperty.Register("RightBorderBrush", typeof(Brush), typeof(FouerColorBorder), new PropertyMetadata(null));

	public static readonly DependencyProperty BottomBorderBrushProperty = DependencyProperty.Register("BottomBorderBrush", typeof(Brush), typeof(FouerColorBorder), new PropertyMetadata(null));

	public Brush LeftBorderBrush
	{
		get
		{
			return (Brush)GetValue(LeftBorderBrushProperty);
		}
		set
		{
			SetValue(LeftBorderBrushProperty, value);
		}
	}

	public Brush TopBorderBrush
	{
		get
		{
			return (Brush)GetValue(TopBorderBrushProperty);
		}
		set
		{
			SetValue(TopBorderBrushProperty, value);
		}
	}

	public Brush RightBorderBrush
	{
		get
		{
			return (Brush)GetValue(RightBorderBrushProperty);
		}
		set
		{
			SetValue(RightBorderBrushProperty, value);
		}
	}

	public Brush BottomBorderBrush
	{
		get
		{
			return (Brush)GetValue(BottomBorderBrushProperty);
		}
		set
		{
			SetValue(BottomBorderBrushProperty, value);
		}
	}

	protected override void OnRender(DrawingContext dc)
	{
		base.OnRender(dc);
		double height = RenderSize.Height;
		double width = RenderSize.Width;
		bool useLayoutRounding = UseLayoutRounding;
		Thickness borderThickness = BorderThickness;
		CornerRadius cornerRadius = CornerRadius;
		double topLeft = cornerRadius.TopLeft;
		Point[][] array = new Point[4][]
		{
			new Point[2],
			new Point[2],
			new Point[2],
			new Point[2]
		};
		Rect[] array2 = new Rect[4];
		bool flag = !DoubleUtil.IsZero(topLeft);
		double num = 0.0;
		if (DoubleUtil.GreaterThan(borderThickness.Top, 0.0))
		{
			num = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Top, DoubleUtil.DpiScaleY) : borderThickness.Top) * 0.5;
		}
		double num2 = 0.0;
		if (DoubleUtil.GreaterThan(borderThickness.Bottom, 0.0))
		{
			num2 = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Bottom, DoubleUtil.DpiScaleY) : borderThickness.Bottom) * 0.5;
		}
		double num3 = 0.0;
		if (DoubleUtil.GreaterThan(borderThickness.Left, 0.0))
		{
			num3 = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Left, DoubleUtil.DpiScaleX) : borderThickness.Left) * 0.5;
		}
		double num4 = 0.0;
		if (DoubleUtil.GreaterThan(borderThickness.Right, 0.0))
		{
			num4 = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Right, DoubleUtil.DpiScaleX) : borderThickness.Right) * 0.5;
		}
		if (flag)
		{
			array2[0].Location = new Point(0.0, 0.0);
			array2[0].Size = new Size(topLeft, topLeft);
			array[0][0].X = topLeft;
			array[0][0].Y = 0.0;
			array[2][1].X = 0.0;
			array[3][1].Y = topLeft;
		}
		double topRight = cornerRadius.TopRight;
		if (!DoubleUtil.IsZero(topRight))
		{
			array2[1].Location = new Point(width - topRight, 0.0);
			array2[1].Size = new Size(topRight, topRight);
			array[0][1].X = array2[1].Location.X;
			array[0][1].Y = array2[1].Location.Y;
			array[1][0].X = width;
			array[1][0].Y = topRight;
		}
		else
		{
			array[0][1].X = width;
			array[0][1].Y = 0.0;
			array[1][0].X = width;
			array[1][0].Y = 0.0;
		}
		double bottomRight = cornerRadius.BottomRight;
		if (!DoubleUtil.IsZero(bottomRight))
		{
			array2[2].Location = new Point(width - bottomRight, height - bottomRight);
			array2[2].Size = new Size(bottomRight, bottomRight);
			array[1][1].X = width;
			array[1][1].Y = array2[2].Location.Y;
			array[2][0].X = width - bottomRight;
			array[2][0].Y = height;
		}
		else
		{
			array[1][1].X = width;
			array[1][1].Y = height;
			array[2][0].X = width;
			array[2][0].Y = height;
		}
		double bottomLeft = cornerRadius.BottomLeft;
		if (!DoubleUtil.IsZero(bottomLeft))
		{
			array2[3].Location = new Point(0.0, height - bottomLeft);
			array2[3].Size = new Size(bottomLeft, bottomLeft);
			array[2][1].X = bottomLeft;
			array[2][1].Y = height;
			array[3][0].X = 0.0;
			array[3][0].Y = height - bottomLeft;
		}
		else
		{
			array[2][1].X = 0.0;
			array[2][1].Y = height;
			array[3][0].X = 0.0;
			array[3][0].Y = height;
		}
		Brush brush = BorderBrush ?? new SolidColorBrush(Colors.Transparent);
		for (int i = 0; i < array.Length; i++)
		{
			Point[] array3 = array[i];
			Pen pen = new Pen();
			StreamGeometry streamGeometry = new StreamGeometry();
			switch (i)
			{
			case 0:
			{
				array3[0].Y += num;
				array3[1].Y += num;
				pen.Brush = TopBorderBrush ?? brush;
				pen.Thickness = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Top, DoubleUtil.DpiScaleY) : borderThickness.Top);
				using (StreamGeometryContext streamGeometryContext4 = streamGeometry.Open())
				{
					streamGeometryContext4.BeginFigure(array3[0], isFilled: false, isClosed: false);
					streamGeometryContext4.LineTo(array3[1], isStroked: true, isSmoothJoin: false);
				}
				break;
			}
			case 1:
			{
				array3[0].X -= num4;
				array3[1].X -= num4;
				pen.Brush = RightBorderBrush ?? brush;
				pen.Thickness = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Right, DoubleUtil.DpiScaleX) : borderThickness.Right);
				using (StreamGeometryContext streamGeometryContext3 = streamGeometry.Open())
				{
					if (array2[1].Width > 0.0)
					{
						Rect rect5 = array2[1];
						Point start5 = rect5.RectToPoints()[(i + 2) % 4];
						start5.X -= num4;
						start5.Y += num;
						bool isLargeArc5 = Math.Abs(45) > 180;
						SweepDirection sweepDirection5 = SweepDirection.Clockwise;
						double width6 = rect5.Width;
						streamGeometryContext3.BeginFigure(start5.GetEndPoint(-90.0, width6), isFilled: false, isClosed: false);
						streamGeometryContext3.ArcTo(start5.GetEndPoint(0.0, width6), new Size(width6, width6), 0.0, isLargeArc5, sweepDirection5, isStroked: true, isSmoothJoin: false);
					}
					else
					{
						streamGeometryContext3.BeginFigure(array3[0], isFilled: false, isClosed: false);
					}
					streamGeometryContext3.LineTo(array3[1], isStroked: true, isSmoothJoin: false);
					if (array2[2].Width > 0.0)
					{
						Rect rect6 = array2[2];
						Point start6 = rect6.RectToPoints()[(i + 3) % 4];
						start6.X -= num4;
						start6.Y -= num2;
						bool isLargeArc6 = Math.Abs(45) > 180;
						SweepDirection sweepDirection6 = SweepDirection.Clockwise;
						double width7 = rect6.Width;
						streamGeometryContext3.BeginFigure(start6.GetEndPoint(0.0, width7), isFilled: false, isClosed: false);
						streamGeometryContext3.ArcTo(start6.GetEndPoint(45.0, width7), new Size(width7, width7), 0.0, isLargeArc6, sweepDirection6, isStroked: true, isSmoothJoin: false);
					}
				}
				break;
			}
			case 2:
			{
				array3[0].Y -= num2;
				array3[1].Y -= num2;
				pen.Brush = BottomBorderBrush ?? brush;
				pen.Thickness = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Bottom, DoubleUtil.DpiScaleY) : borderThickness.Bottom);
				using (StreamGeometryContext streamGeometryContext2 = streamGeometry.Open())
				{
					if (array2[2].Width > 0.0)
					{
						Rect rect3 = array2[2];
						Point start3 = rect3.RectToPoints()[(i + 2) % 4];
						start3.X -= num4;
						start3.Y -= num2;
						bool isLargeArc3 = Math.Abs(45) > 180;
						SweepDirection sweepDirection3 = SweepDirection.Clockwise;
						double width4 = rect3.Width;
						streamGeometryContext2.BeginFigure(start3.GetEndPoint(45.0, width4), isFilled: false, isClosed: false);
						streamGeometryContext2.ArcTo(start3.GetEndPoint(90.0, width4), new Size(width4, width4), 0.0, isLargeArc3, sweepDirection3, isStroked: true, isSmoothJoin: false);
					}
					else
					{
						streamGeometryContext2.BeginFigure(array3[0], isFilled: false, isClosed: false);
					}
					streamGeometryContext2.LineTo(array3[1], isStroked: true, isSmoothJoin: false);
					if (array2[3].Width > 0.0)
					{
						Rect rect4 = array2[3];
						Point start4 = rect4.RectToPoints()[(i + 3) % 4];
						start4.X += num3;
						start4.Y -= num2;
						bool isLargeArc4 = Math.Abs(45) > 180;
						SweepDirection sweepDirection4 = SweepDirection.Clockwise;
						double width5 = rect4.Width;
						streamGeometryContext2.BeginFigure(start4.GetEndPoint(90.0, width5), isFilled: false, isClosed: false);
						streamGeometryContext2.ArcTo(start4.GetEndPoint(135.0, width5), new Size(width5, width5), 0.0, isLargeArc4, sweepDirection4, isStroked: true, isSmoothJoin: false);
					}
				}
				break;
			}
			case 3:
			{
				array3[0].X += num3;
				array3[1].X += num3;
				pen.Brush = LeftBorderBrush ?? brush;
				pen.Thickness = (useLayoutRounding ? UlementEx.RoundLayoutValue(borderThickness.Left, DoubleUtil.DpiScaleX) : borderThickness.Left);
				using (StreamGeometryContext streamGeometryContext = streamGeometry.Open())
				{
					if (array2[3].Width > 0.0)
					{
						Rect rect = array2[3];
						Point start = rect.RectToPoints()[(i + 2) % 4];
						start.X += num3;
						start.Y -= num2;
						bool isLargeArc = Math.Abs(45) > 180;
						SweepDirection sweepDirection = SweepDirection.Clockwise;
						double width2 = rect.Width;
						streamGeometryContext.BeginFigure(start.GetEndPoint(135.0, width2), isFilled: false, isClosed: false);
						streamGeometryContext.ArcTo(start.GetEndPoint(180.0, width2), new Size(width2, width2), 0.0, isLargeArc, sweepDirection, isStroked: true, isSmoothJoin: false);
					}
					else
					{
						streamGeometryContext.BeginFigure(array3[0], isFilled: false, isClosed: false);
					}
					streamGeometryContext.LineTo(array3[1], isStroked: true, isSmoothJoin: false);
					if (array2[0].Width > 0.0)
					{
						Rect rect2 = array2[0];
						Point start2 = rect2.RectToPoints()[(i + 3) % 4];
						start2.X += num3;
						start2.Y += num;
						bool isLargeArc2 = Math.Abs(45) > 180;
						SweepDirection sweepDirection2 = SweepDirection.Clockwise;
						double width3 = rect2.Width;
						streamGeometryContext.BeginFigure(start2.GetEndPoint(180.0, width3), isFilled: false, isClosed: false);
						streamGeometryContext.ArcTo(start2.GetEndPoint(270.0, width3), new Size(width3, width3), 0.0, isLargeArc2, sweepDirection2, isStroked: true, isSmoothJoin: false);
					}
				}
				break;
			}
			}
			pen.Thickness *= 1.2;
			pen.Freeze();
			dc.DrawGeometry(pen.Brush, pen, streamGeometry);
		}
	}
}
