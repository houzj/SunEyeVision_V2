using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Release;

public class VMWpfUserControl : UserControl
{
	static VMWpfUserControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(VMWpfUserControl), new FrameworkPropertyMetadata(typeof(VMWpfUserControl)));
	}
}
