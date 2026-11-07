using System.Windows;

namespace VM.Controls.PropertyGrid;

public class PropertyValueChangedEventArgs : RoutedEventArgs
{
	public object NewValue { get; set; }

	public object OldValue { get; set; }

	public PropertyValueChangedEventArgs(RoutedEvent routedEvent, object source, object oldValue, object newValue)
		: base(routedEvent, source)
	{
		NewValue = newValue;
		OldValue = oldValue;
	}
}
