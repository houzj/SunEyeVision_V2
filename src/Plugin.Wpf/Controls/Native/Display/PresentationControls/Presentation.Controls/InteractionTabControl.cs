using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class InteractionTabControl : TabControl
{
	public static readonly DependencyProperty ExpanderDirectionProperty;

	public ExpandDirection ExpanderDirection
	{
		get
		{
			return (ExpandDirection)GetValue(ExpanderDirectionProperty);
		}
		set
		{
			SetValue(ExpanderDirectionProperty, value);
		}
	}

	static InteractionTabControl()
	{
		ExpanderDirectionProperty = DependencyProperty.Register("ExpanderDirection", typeof(ExpandDirection), typeof(InteractionTabControl), new PropertyMetadata(ExpandDirection.LeftToRight));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(InteractionTabControl), new FrameworkPropertyMetadata(typeof(InteractionTabControl)));
	}
}
