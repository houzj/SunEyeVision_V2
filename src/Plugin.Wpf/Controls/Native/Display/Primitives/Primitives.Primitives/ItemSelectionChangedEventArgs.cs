using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Primitives;

public class ItemSelectionChangedEventArgs : RoutedEventArgs
{
	public bool IsSelected { get; private set; }

	public object Item { get; private set; }

	public ItemSelectionChangedEventArgs(RoutedEvent routedEvent, object source, object item, bool isSelected)
		: base(routedEvent, source)
	{
		Item = item;
		IsSelected = isSelected;
	}
}
