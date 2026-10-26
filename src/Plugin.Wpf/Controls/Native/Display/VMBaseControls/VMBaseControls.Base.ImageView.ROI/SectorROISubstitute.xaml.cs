using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class SectorROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(Point), typeof(SectorROISubstitute), new PropertyMetadata(new Point(double.NaN, double.NaN), OnCenterPointChanged));

	public static readonly DependencyProperty StartAngleProperty = DependencyProperty.Register("StartAngle", typeof(double), typeof(SectorROISubstitute), new PropertyMetadata(0.0, StartAngleChanged));

	public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(SectorROISubstitute), new PropertyMetadata(double.NaN, OnRadiusChanged));

	private PathFigure pFigure;

	private LineSegment lSegment;

	private ArcSegment aSegment;

	public Point CenterPoint
	{
		get
		{
			return (Point)GetValue(CenterPointProperty);
		}
		set
		{
			SetValue(CenterPointProperty, value);
		}
	}

	public double StartAngle
	{
		get
		{
			return (double)GetValue(StartAngleProperty);
		}
		set
		{
			SetValue(StartAngleProperty, value);
		}
	}

	public double Radius
	{
		get
		{
			return (double)GetValue(RadiusProperty);
		}
		set
		{
			SetValue(RadiusProperty, value);
		}
	}

	private static void OnCenterPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROISubstitute sectorROISubstitute = d as SectorROISubstitute;
		if (sectorROISubstitute.pFigure != null)
		{
			sectorROISubstitute.pFigure.StartPoint = (Point)e.NewValue;
		}
		if (sectorROISubstitute.lSegment != null)
		{
			sectorROISubstitute.lSegment.Point = sectorROISubstitute.GetBottomPoint();
		}
		if (sectorROISubstitute.aSegment != null)
		{
			sectorROISubstitute.aSegment.Point = sectorROISubstitute.GetRightPoint();
		}
	}

	private static void StartAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROISubstitute sectorROISubstitute = d as SectorROISubstitute;
	}

	private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SectorROISubstitute sectorROISubstitute = d as SectorROISubstitute;
		if (sectorROISubstitute.lSegment != null)
		{
			sectorROISubstitute.lSegment.Point = sectorROISubstitute.GetBottomPoint();
		}
		if (sectorROISubstitute.aSegment != null)
		{
			sectorROISubstitute.aSegment.Size = sectorROISubstitute.GetArcSize();
			sectorROISubstitute.aSegment.Point = sectorROISubstitute.GetRightPoint();
		}
	}

	private Point GetRightPoint()
	{
		if (double.IsNaN(Radius) || double.IsNaN(CenterPoint.X) || double.IsNaN(CenterPoint.Y))
		{
			return new Point(double.NaN, double.NaN);
		}
		RotateTransform rotateTransform = new RotateTransform();
		rotateTransform.Angle = StartAngle;
		rotateTransform.CenterX = CenterPoint.X;
		rotateTransform.CenterY = CenterPoint.Y;
		Point point = new Point(CenterPoint.X + Radius, CenterPoint.Y);
		return rotateTransform.Transform(point);
	}

	private Point GetBottomPoint()
	{
		if (double.IsNaN(Radius) || double.IsNaN(CenterPoint.X) || double.IsNaN(CenterPoint.Y))
		{
			return new Point(double.NaN, double.NaN);
		}
		RotateTransform rotateTransform = new RotateTransform();
		rotateTransform.Angle = StartAngle;
		rotateTransform.CenterX = CenterPoint.X;
		rotateTransform.CenterY = CenterPoint.Y;
		Point point = new Point(CenterPoint.X, CenterPoint.Y + Radius);
		return rotateTransform.Transform(point);
	}

	private Size GetArcSize()
	{
		return new Size(Radius, Radius);
	}

	public SectorROISubstitute()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		pFigure = Template.FindName("pFigure", this) as PathFigure;
		pFigure.StartPoint = CenterPoint;
		lSegment = Template.FindName("lSegment", this) as LineSegment;
		lSegment.Point = GetBottomPoint();
		aSegment = Template.FindName("aSegment", this) as ArcSegment;
		aSegment.Size = GetArcSize();
		aSegment.Point = GetRightPoint();
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "4.0.0.0")]
	internal Delegate _CreateDelegate(Type delegateType, string handler)
	{
		return Delegate.CreateDelegate(delegateType, this, handler);
	}
}
