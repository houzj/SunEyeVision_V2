using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

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
