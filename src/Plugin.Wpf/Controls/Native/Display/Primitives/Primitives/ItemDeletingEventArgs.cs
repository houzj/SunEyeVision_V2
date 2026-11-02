using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class ItemDeletingEventArgs : CancelRoutedEventArgs
{
	private object _item;

	public object Item => _item;

	public ItemDeletingEventArgs(RoutedEvent itemDeletingEvent, object itemDeleting)
		: base(itemDeletingEvent)
	{
		_item = itemDeleting;
	}
}
