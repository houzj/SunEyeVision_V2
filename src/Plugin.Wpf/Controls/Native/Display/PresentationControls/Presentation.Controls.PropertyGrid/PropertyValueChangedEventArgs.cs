using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid;

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
