using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

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
