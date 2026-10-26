using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

public class PropertyChangedEventArgs<T> : RoutedEventArgs
{
	private readonly T _newValue;

	private readonly T _oldValue;

	public T NewValue => _newValue;

	public T OldValue => _oldValue;

	public PropertyChangedEventArgs(RoutedEvent Event, T oldValue, T newValue)
	{
		_oldValue = oldValue;
		_newValue = newValue;
		RoutedEvent = Event;
	}

	protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
	{
		((PropertyChangedEventHandler<T>)genericHandler)(genericTarget, this);
	}
}
