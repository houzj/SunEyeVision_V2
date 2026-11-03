using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

public class CancelRoutedEventArgs : RoutedEventArgs
{
	public bool Cancel { get; set; }

	public CancelRoutedEventArgs()
	{
	}

	public CancelRoutedEventArgs(RoutedEvent routedEvent)
		: base(routedEvent)
	{
	}

	public CancelRoutedEventArgs(RoutedEvent routedEvent, object source)
		: base(routedEvent, source)
	{
	}
}
