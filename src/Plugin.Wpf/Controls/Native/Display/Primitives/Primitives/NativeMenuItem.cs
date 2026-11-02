using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class NativeMenuItem : MenuItem
{
	static NativeMenuItem()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeMenuItem), new FrameworkPropertyMetadata(typeof(NativeMenuItem)));
	}
}
