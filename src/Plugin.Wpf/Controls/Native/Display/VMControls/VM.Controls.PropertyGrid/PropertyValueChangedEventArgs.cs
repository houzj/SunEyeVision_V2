using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

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
