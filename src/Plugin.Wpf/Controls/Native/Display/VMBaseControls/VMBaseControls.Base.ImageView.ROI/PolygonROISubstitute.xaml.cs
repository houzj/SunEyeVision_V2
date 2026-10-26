using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class PolygonROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty PointsProperty = DependencyProperty.Register("Points", typeof(PointCollection), typeof(PolygonROISubstitute), new PropertyMetadata(null));

	public PointCollection Points
	{
		get
		{
			return (PointCollection)GetValue(PointsProperty);
		}
		set
		{
			SetValue(PointsProperty, value);
		}
	}

	public PolygonROISubstitute()
	{
		InitializeComponent();
	}
}
