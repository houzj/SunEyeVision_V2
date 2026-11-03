using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core;

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
