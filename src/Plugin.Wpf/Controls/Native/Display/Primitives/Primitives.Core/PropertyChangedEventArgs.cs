using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.Core;

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
