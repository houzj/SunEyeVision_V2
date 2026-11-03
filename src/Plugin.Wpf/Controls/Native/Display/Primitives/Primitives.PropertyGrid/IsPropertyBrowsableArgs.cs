using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;

public class IsPropertyBrowsableArgs : RoutedEventArgs
{
	public bool? IsBrowsable { get; set; }

	public PropertyDescriptor PropertyDescriptor { get; private set; }

	public IsPropertyBrowsableArgs(PropertyDescriptor pd)
	{
		PropertyDescriptor = pd;
	}
}
