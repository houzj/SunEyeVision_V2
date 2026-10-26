using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public sealed class Arrow : System.Windows.Shapes.Shape
{
	public static readonly DependencyProperty X1Property = DependencyProperty.Register("X1", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty Y1Property = DependencyProperty.Register("Y1", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty X2Property = DependencyProperty.Register("X2", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty Y2Property = DependencyProperty.Register("Y2", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty HeadWidthProperty = DependencyProperty.Register("HeadWidth", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	public static readonly DependencyProperty HeadHeightProperty = DependencyProperty.Register("HeadHeight", typeof(double), typeof(Arrow), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

	[TypeConverter(typeof(LengthConverter))]
	public double X1
	{
		get
		{
			return (double)GetValue(X1Property);
		}
		set
		{
			SetValue(X1Property, value);
		}
	}

	[TypeConverter(typeof(LengthConverter))]
	public double Y1
	{
		get
		{
			return (double)GetValue(Y1Property);
		}
		set
		{
			SetValue(Y1Property, value);
		}
	}

	[TypeConverter(typeof(LengthConverter))]
	public double X2
	{
		get
		{
			return (double)GetValue(X2Property);
		}
		set
		{
			SetValue(X2Property, value);
		}
	}

	[TypeConverter(typeof(LengthConverter))]
	public double Y2
	{
		get
		{
			return (double)GetValue(Y2Property);
		}
		set
		{
			SetValue(Y2Property, value);
		}
	}

	[TypeConverter(typeof(LengthConverter))]
	public double HeadWidth
	{
		get
		{
			return (double)GetValue(HeadWidthProperty);
		}
		set
		{
			SetValue(HeadWidthProperty, value);
		}
	}

	[TypeConverter(typeof(LengthConverter))]
	public double HeadHeight
	{
		get
		{
			return (double)GetValue(HeadHeightProperty);
		}
		set
		{
			SetValue(HeadHeightProperty, value);
		}
	}

	protected override Geometry DefiningGeometry
	{
		get
		{
			StreamGeometry streamGeometry = new StreamGeometry();
			streamGeometry.FillRule = FillRule.EvenOdd;
			using (StreamGeometryContext context = streamGeometry.Open())
			{
				InternalDrawArrowGeometry(context);
			}
			streamGeometry.Freeze();
			return streamGeometry;
		}
	}

	private void InternalDrawArrowGeometry(StreamGeometryContext context)
	{
		double num = Math.Atan2(Y1 - Y2, X1 - X2);
		double num2 = Math.Sin(num);
		double num3 = Math.Cos(num);
		Point startPoint = new Point(X1, Y1);
		Point point = new Point(X2, Y2);
		Point startPoint2 = new Point(X2 + (HeadWidth * num3 - HeadHeight * num2), Y2 + (HeadWidth * num2 + HeadHeight * num3));
		Point point2 = new Point(X2 + (HeadWidth * num3 + HeadHeight * num2), Y2 - (HeadHeight * num3 - HeadWidth * num2));
		context.BeginFigure(startPoint, isFilled: true, isClosed: false);
		context.LineTo(point, isStroked: true, isSmoothJoin: true);
		context.BeginFigure(startPoint2, isFilled: true, isClosed: true);
		context.LineTo(point2, isStroked: true, isSmoothJoin: true);
		context.LineTo(point, isStroked: true, isSmoothJoin: true);
	}
}
