using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;

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
