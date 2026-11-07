using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;

namespace VMBaseControls.Base.ImageView.ROI;

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
