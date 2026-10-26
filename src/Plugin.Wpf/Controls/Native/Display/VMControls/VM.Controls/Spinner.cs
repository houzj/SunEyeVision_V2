using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public abstract class Spinner : Control
{
	public static readonly DependencyProperty ValidSpinDirectionProperty = DependencyProperty.Register("ValidSpinDirection", typeof(ValidSpinDirections), typeof(Spinner), new PropertyMetadata(ValidSpinDirections.Increase | ValidSpinDirections.Decrease, OnValidSpinDirectionPropertyChanged));

	public static readonly RoutedEvent SpinnerSpinEvent = EventManager.RegisterRoutedEvent("SpinnerSpin", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(Spinner));

	public ValidSpinDirections ValidSpinDirection
	{
		get
		{
			return (ValidSpinDirections)GetValue(ValidSpinDirectionProperty);
		}
		set
		{
			SetValue(ValidSpinDirectionProperty, value);
		}
	}

	public event EventHandler<SpinEventArgs> Spin;

	public event RoutedEventHandler SpinnerSpin
	{
		add
		{
			AddHandler(SpinnerSpinEvent, value);
		}
		remove
		{
			RemoveHandler(SpinnerSpinEvent, value);
		}
	}

	private static void OnValidSpinDirectionPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		Spinner spinner = (Spinner)d;
		ValidSpinDirections oldValue = (ValidSpinDirections)e.OldValue;
		ValidSpinDirections newValue = (ValidSpinDirections)e.NewValue;
		spinner.OnValidSpinDirectionChanged(oldValue, newValue);
	}

	protected virtual void OnSpin(SpinEventArgs e)
	{
		ValidSpinDirections validSpinDirections = ((e.Direction == SpinDirection.Increase) ? ValidSpinDirections.Increase : ValidSpinDirections.Decrease);
		if ((ValidSpinDirection & validSpinDirections) == validSpinDirections)
		{
			Spin?.Invoke(this, e);
		}
	}

	protected virtual void OnValidSpinDirectionChanged(ValidSpinDirections oldValue, ValidSpinDirections newValue)
	{
	}
}
