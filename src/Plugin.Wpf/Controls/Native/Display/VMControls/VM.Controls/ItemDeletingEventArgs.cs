using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

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
