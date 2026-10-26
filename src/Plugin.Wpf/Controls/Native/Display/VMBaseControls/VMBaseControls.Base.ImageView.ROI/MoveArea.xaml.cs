using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class MoveArea : Control, IComponentConnector
{
	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register("Position", typeof(Point), typeof(MoveArea), new PropertyMetadata(default(Point), OnPositionChanged));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(MoveArea), new PropertyMetadata(0.0));

	public static readonly DependencyProperty RectMarginProperty = DependencyProperty.Register("RectMargin", typeof(Thickness), typeof(MoveArea), new PropertyMetadata(default(Thickness)));

	public Point Position
	{
		get
		{
			return (Point)GetValue(PositionProperty);
		}
		set
		{
			SetValue(PositionProperty, value);
		}
	}

	public double StrokeThickness
	{
		get
		{
			return (double)GetValue(StrokeThicknessProperty);
		}
		set
		{
			SetValue(StrokeThicknessProperty, value);
		}
	}

	public Thickness RectMargin
	{
		get
		{
			return (Thickness)GetValue(RectMarginProperty);
		}
		set
		{
			SetValue(RectMarginProperty, value);
		}
	}

	private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		try
		{
			Point point = (Point)e.NewValue;
			MoveArea element = d as MoveArea;
			if (IsInvaildDouble(point.X))
			{
				Canvas.SetLeft(element, point.X);
			}
			if (IsInvaildDouble(point.Y))
			{
				Canvas.SetTop(element, point.Y);
			}
		}
		catch (Exception)
		{
		}
	}

	private static bool IsInvaildDouble(double x)
	{
		return x != double.NaN && x != double.NegativeInfinity && x != double.PositiveInfinity;
	}

	public MoveArea()
	{
		InitializeComponent();
	}
}
