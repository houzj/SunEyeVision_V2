using System.Windows;
using VM.Controls.Core;

namespace VM.Controls;

public class ItemAddingEventArgs : CancelRoutedEventArgs
{
	public object Item { get; set; }

	public ItemAddingEventArgs(RoutedEvent itemAddingEvent, object itemAdding)
		: base(itemAddingEvent)
	{
		Item = itemAdding;
	}
}
