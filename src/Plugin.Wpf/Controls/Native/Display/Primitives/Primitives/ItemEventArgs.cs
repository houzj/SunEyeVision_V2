using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

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
