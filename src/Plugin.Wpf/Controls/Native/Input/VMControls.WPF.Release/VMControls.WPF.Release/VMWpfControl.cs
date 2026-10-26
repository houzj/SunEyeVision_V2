using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Release;

public class VMWpfControl : Control
{
	static VMWpfControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(VMWpfControl), new FrameworkPropertyMetadata(typeof(VMWpfControl)));
	}
}
