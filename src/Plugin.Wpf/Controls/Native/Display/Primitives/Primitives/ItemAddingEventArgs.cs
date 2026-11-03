using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class ItemAddingEventArgs : CancelRoutedEventArgs
{
	public object Item { get; set; }

	public ItemAddingEventArgs(RoutedEvent itemAddingEvent, object itemAdding)
		: base(itemAddingEvent)
	{
		Item = itemAdding;
	}
}
