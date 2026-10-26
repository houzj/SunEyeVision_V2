using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

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
