using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class HikScrollBar : ScrollBar
{
	static HikScrollBar()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikScrollBar), new FrameworkPropertyMetadata(typeof(HikScrollBar)));
	}
}
