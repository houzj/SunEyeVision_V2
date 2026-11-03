using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;

public class ItemAddingEventArgs : CancelRoutedEventArgs
{
	public object Item { get; set; }

	public ItemAddingEventArgs(RoutedEvent itemAddingEvent, object itemAdding)
		: base(itemAddingEvent)
	{
		Item = itemAdding;
	}
}
