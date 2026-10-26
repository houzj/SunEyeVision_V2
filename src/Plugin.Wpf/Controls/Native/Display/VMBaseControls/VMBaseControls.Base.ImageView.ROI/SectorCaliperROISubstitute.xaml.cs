using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class SectorCaliperROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty StartProperty = DependencyProperty.Register("Start", typeof(Point), typeof(SectorCaliperROISubstitute), new PropertyMetadata(default(Point), OnStartChanged));

	public static readonly DependencyProperty EndProperty = DependencyProperty.Register("End", typeof(Point), typeof(SectorCaliperROISubstitute), new PropertyMetadata(default(Point), OnEndChanged));

	public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(SectorCaliperROISubstitute), new PropertyMetadata(OnRadiusChanged));

	private PathFigure PFigure;

	private ArcSegment ASegment;

	public Point Start
	{
		get
		{
			return (Point)GetValue(StartProperty);
		}
		set
		{
			SetValue(StartProperty, value);
		}
	}

	public Point End
	{
		get
		{
			return (Point)GetValue(EndProperty);
		}
		set
		{
			SetValue(EndProperty, value);
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

	private static void OnStartChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROISubstitute { PFigure: not null } sectorCaliperROISubstitute)
		{
			sectorCaliperROISubstitute.PFigure.StartPoint = (Point)e.NewValue;
		}
	}

	private static void OnEndChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROISubstitute { ASegment: not null } sectorCaliperROISubstitute)
		{
			sectorCaliperROISubstitute.ASegment.Point = (Point)e.NewValue;
		}
	}

	private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is SectorCaliperROISubstitute { ASegment: not null } sectorCaliperROISubstitute)
		{
			double num = (double)e.NewValue;
			sectorCaliperROISubstitute.ASegment.Size = new Size(num, num);
		}
	}

	public SectorCaliperROISubstitute()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		PFigure = Template.FindName("PFigure", this) as PathFigure;
		PFigure.StartPoint = Start;
		ASegment = Template.FindName("ASegment", this) as ArcSegment;
		ASegment.Size = new Size(Radius, Radius);
		ASegment.Point = End;
	}

}
