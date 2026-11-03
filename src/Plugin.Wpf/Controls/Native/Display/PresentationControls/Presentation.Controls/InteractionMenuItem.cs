using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class InteractionMenuItem : MenuItem
{
	static InteractionMenuItem()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(InteractionMenuItem), new FrameworkPropertyMetadata(typeof(InteractionMenuItem)));
	}
}
