using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class CircleROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty CenterPointProperty = DependencyProperty.Register("CenterPoint", typeof(Point), typeof(CircleROISubstitute), new PropertyMetadata(default(Point)));

	public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(CircleROISubstitute), new PropertyMetadata(0.0));

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

	public CircleROISubstitute()
	{
		InitializeComponent();
	}
}
