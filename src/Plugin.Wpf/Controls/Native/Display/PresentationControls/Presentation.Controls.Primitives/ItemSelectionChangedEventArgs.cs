using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Primitives;

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
