using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public class IsPropertyBrowsableArgs : RoutedEventArgs
{
	public bool? IsBrowsable { get; set; }

	public PropertyDescriptor PropertyDescriptor { get; private set; }

	public IsPropertyBrowsableArgs(PropertyDescriptor pd)
	{
		PropertyDescriptor = pd;
	}
}
