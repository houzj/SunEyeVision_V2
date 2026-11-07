using System.Windows;

namespace VM.Controls.PropertyGrid;

public class PropertyItemEventArgs : RoutedEventArgs
{
	public PropertyItemBase PropertyItem { get; private set; }

	public object Item { get; private set; }

	public PropertyItemEventArgs(RoutedEvent routedEvent, object source, PropertyItemBase propertyItem, object item)
		: base(routedEvent, source)
	{
		PropertyItem = propertyItem;
		Item = item;
	}
}
