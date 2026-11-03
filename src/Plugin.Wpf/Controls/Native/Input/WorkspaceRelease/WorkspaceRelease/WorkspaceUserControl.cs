using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceRelease;

public class WorkspaceUserControl : UserControl
{
	static WorkspaceUserControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(WorkspaceUserControl), new FrameworkPropertyMetadata(typeof(WorkspaceUserControl)));
	}
}
