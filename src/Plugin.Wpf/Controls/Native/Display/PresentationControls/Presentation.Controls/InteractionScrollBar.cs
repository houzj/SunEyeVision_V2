using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class InteractionScrollBar : ScrollBar
{
	static InteractionScrollBar()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(InteractionScrollBar), new FrameworkPropertyMetadata(typeof(InteractionScrollBar)));
	}
}
