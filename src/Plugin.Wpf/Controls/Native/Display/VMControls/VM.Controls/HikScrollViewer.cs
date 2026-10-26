using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class HikScrollViewer : ScrollViewer
{
	static HikScrollViewer()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(HikScrollViewer), new FrameworkPropertyMetadata(typeof(HikScrollViewer)));
	}
}
