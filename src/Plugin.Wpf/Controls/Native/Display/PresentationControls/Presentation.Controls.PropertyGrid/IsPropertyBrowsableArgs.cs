using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid;

public class IsPropertyBrowsableArgs : RoutedEventArgs
{
	public bool? IsBrowsable { get; set; }

	public PropertyDescriptor PropertyDescriptor { get; private set; }

	public IsPropertyBrowsableArgs(PropertyDescriptor pd)
	{
		PropertyDescriptor = pd;
	}
}
