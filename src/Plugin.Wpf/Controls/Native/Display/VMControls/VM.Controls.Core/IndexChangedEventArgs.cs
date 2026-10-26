using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

public class IndexChangedEventArgs : PropertyChangedEventArgs<int>
{
	public IndexChangedEventArgs(RoutedEvent routedEvent, int oldIndex, int newIndex)
		: base(routedEvent, oldIndex, newIndex)
	{
	}

	protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
	{
		((IndexChangedEventHandler)genericHandler)(genericTarget, this);
	}
}
