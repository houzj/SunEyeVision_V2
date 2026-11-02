using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives;

public class NativeScrollBar : ScrollBar
{
	static NativeScrollBar()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(NativeScrollBar), new FrameworkPropertyMetadata(typeof(NativeScrollBar)));
	}
}
