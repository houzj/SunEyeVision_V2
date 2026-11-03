using SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Coordinates;
using System.Windows;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Parameters.Editors;

public partial class BoxROIStack : ROIStackControl, IComponentConnector
{
	public static readonly DependencyProperty IsRotatableProperty = DependencyProperty.Register("IsRotatable", typeof(bool), typeof(BoxROIStack), new PropertyMetadata(true));

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

	public BoxROIStack()
	{
		InitializeComponent();
	}
}
