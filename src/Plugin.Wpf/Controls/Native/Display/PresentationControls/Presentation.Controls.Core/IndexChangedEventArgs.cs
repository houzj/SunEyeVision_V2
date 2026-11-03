using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.Core;

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
