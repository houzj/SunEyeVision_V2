using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public partial class SectorROIStack : ROIStackControl, IComponentConnector
{
	public static readonly DependencyProperty MaxAngleRangeProperty = DependencyProperty.Register("MaxAngleRange", typeof(double), typeof(SectorROIStack), new PropertyMetadata(360.0));

	public double MaxAngleRange
	{
		get
		{
			return (double)GetValue(MaxAngleRangeProperty);
		}
		set
		{
			SetValue(MaxAngleRangeProperty, value);
		}
	}

	public SectorROIStack()
	{
		InitializeComponent();
	}
}
