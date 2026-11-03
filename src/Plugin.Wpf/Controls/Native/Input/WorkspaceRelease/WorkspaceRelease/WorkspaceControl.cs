using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Input.WorkspaceRelease;

public class WorkspaceControl : Control
{
	static WorkspaceControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(WorkspaceControl), new FrameworkPropertyMetadata(typeof(WorkspaceControl)));
	}
}
