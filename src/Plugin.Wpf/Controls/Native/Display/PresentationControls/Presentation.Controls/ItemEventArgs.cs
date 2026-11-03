using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

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
