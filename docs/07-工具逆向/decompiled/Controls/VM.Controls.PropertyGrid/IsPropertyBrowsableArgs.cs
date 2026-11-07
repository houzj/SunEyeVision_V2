using System.ComponentModel;
using System.Windows;

namespace VM.Controls.PropertyGrid;

public class IsPropertyBrowsableArgs : RoutedEventArgs
{
	public bool? IsBrowsable { get; set; }

	public PropertyDescriptor PropertyDescriptor { get; private set; }

	public IsPropertyBrowsableArgs(PropertyDescriptor pd)
	{
		PropertyDescriptor = pd;
	}
}
