using System.Windows;
using VM.Controls.Core;

namespace VM.Controls;

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
