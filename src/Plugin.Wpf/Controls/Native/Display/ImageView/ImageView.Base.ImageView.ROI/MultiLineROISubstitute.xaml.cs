using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Display.ImageView.Base.ImageView.ROI;

public partial class MultiLineROISubstitute : ROISubstitute, IComponentConnector
{
	public static readonly DependencyProperty PointsProperty = DependencyProperty.Register("Points", typeof(PointCollection), typeof(MultiLineROISubstitute), new PropertyMetadata(null));

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

	public MultiLineROISubstitute()
	{
		InitializeComponent();
	}
}
