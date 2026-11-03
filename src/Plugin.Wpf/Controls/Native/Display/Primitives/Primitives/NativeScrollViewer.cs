using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class NativeScrollViewer : ScrollViewer
{
	static NativeScrollViewer()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeScrollViewer), new FrameworkPropertyMetadata(typeof(NativeScrollViewer)));
	}
}
