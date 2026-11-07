using System.Windows;

namespace VM.Controls.Core;

public class CancelRoutedEventArgs : RoutedEventArgs
{
	public bool Cancel { get; set; }

	public CancelRoutedEventArgs()
	{
	}

	public CancelRoutedEventArgs(RoutedEvent routedEvent)
		: base(routedEvent)
	{
	}

	public CancelRoutedEventArgs(RoutedEvent routedEvent, object source)
		: base(routedEvent, source)
	{
	}
}
