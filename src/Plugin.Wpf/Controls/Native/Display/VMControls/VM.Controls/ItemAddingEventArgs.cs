using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class ItemAddingEventArgs : CancelRoutedEventArgs
{
	public object Item { get; set; }

	public ItemAddingEventArgs(RoutedEvent itemAddingEvent, object itemAdding)
		: base(itemAddingEvent)
	{
		Item = itemAdding;
	}
}
