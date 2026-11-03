using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

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
