using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class EllipseROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(Point), typeof(EllipseROISubstitute), new PropertyMetadata(default(Point)));

	public static readonly DependencyProperty RadiusXProperty = DependencyProperty.Register("RadiusX", typeof(double), typeof(EllipseROISubstitute), new PropertyMetadata(0.0));

	public static readonly DependencyProperty RadiusYProperty = DependencyProperty.Register("RadiusY", typeof(double), typeof(EllipseROISubstitute), new PropertyMetadata(0.0));

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

	public double RadiusX
	{
		get
		{
			return (double)GetValue(RadiusXProperty);
		}
		set
		{
			SetValue(RadiusXProperty, value);
		}
	}

	public double RadiusY
	{
		get
		{
			return (double)GetValue(RadiusYProperty);
		}
		set
		{
			SetValue(RadiusYProperty, value);
		}
	}

	public EllipseROISubstitute()
	{
		InitializeComponent();
	}
}
