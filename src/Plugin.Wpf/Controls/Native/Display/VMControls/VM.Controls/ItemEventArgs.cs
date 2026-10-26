using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class ItemEventArgs : RoutedEventArgs
{
	private object _item;

	public object Item => _item;

	internal ItemEventArgs(RoutedEvent routedEvent, object newItem)
		: base(routedEvent)
	{
		_item = newItem;
	}
}
