using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public partial class CoordinateROIStack : ROIStackControl, IComponentConnector
{
	public static readonly DependencyProperty IsRotatableProperty = DependencyProperty.Register("IsRotatable", typeof(bool), typeof(CoordinateROIStack), new PropertyMetadata(true));

	public bool IsRotatable
	{
		get
		{
			return (bool)GetValue(IsRotatableProperty);
		}
		set
		{
			SetValue(IsRotatableProperty, value);
		}
	}

	public CoordinateROIStack()
	{
		InitializeComponent();
	}
}
