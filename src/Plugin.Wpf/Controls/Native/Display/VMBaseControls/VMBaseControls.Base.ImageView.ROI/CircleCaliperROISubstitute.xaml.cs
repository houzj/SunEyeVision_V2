using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class CircleCaliperROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(Point), typeof(CircleCaliperROISubstitute), new PropertyMetadata(default(Point), OnCenterPointChanged));

	public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(CircleCaliperROISubstitute), new PropertyMetadata(0.0, OnRadiusChanged));

	private EllipseGeometry Eg;

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
		if (d is CircleCaliperROISubstitute { Eg: not null } circleCaliperROISubstitute)
		{
			circleCaliperROISubstitute.Eg.Center = (Point)e.NewValue;
		}
	}

	private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is CircleCaliperROISubstitute { Eg: not null } circleCaliperROISubstitute)
		{
			circleCaliperROISubstitute.Eg.RadiusX = (double)e.NewValue;
			circleCaliperROISubstitute.Eg.RadiusY = circleCaliperROISubstitute.Eg.RadiusX;
		}
	}

	public CircleCaliperROISubstitute()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		Eg = Template.FindName("Eg", this) as EllipseGeometry;
		Eg.Center = CenterPoint;
		Eg.RadiusX = Radius;
		Eg.RadiusY = Eg.RadiusX;
	}
}
