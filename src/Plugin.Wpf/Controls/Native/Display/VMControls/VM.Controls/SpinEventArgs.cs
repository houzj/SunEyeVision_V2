using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class SpinEventArgs : RoutedEventArgs
{
	public SpinDirection Direction { get; private set; }

	public bool UsingMouseWheel { get; private set; }

	public SpinEventArgs(SpinDirection direction)
	{
		Direction = direction;
	}

	public SpinEventArgs(RoutedEvent routedEvent, SpinDirection direction)
		: base(routedEvent)
	{
		Direction = direction;
	}

	public SpinEventArgs(SpinDirection direction, bool usingMouseWheel)
	{
		Direction = direction;
		UsingMouseWheel = usingMouseWheel;
	}

	public SpinEventArgs(RoutedEvent routedEvent, SpinDirection direction, bool usingMouseWheel)
		: base(routedEvent)
	{
		Direction = direction;
		UsingMouseWheel = usingMouseWheel;
	}
}
