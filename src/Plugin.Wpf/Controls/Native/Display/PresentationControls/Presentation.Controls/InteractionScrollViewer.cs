using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class InteractionScrollViewer : ScrollViewer
{
	static InteractionScrollViewer()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(InteractionScrollViewer), new FrameworkPropertyMetadata(typeof(InteractionScrollViewer)));
	}
}
