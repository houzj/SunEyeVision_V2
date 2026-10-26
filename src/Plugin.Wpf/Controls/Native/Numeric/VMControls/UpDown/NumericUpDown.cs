using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;
using System.Globalization;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

public abstract class NumericUpDown<T> : UpDownBase<T>
{
	public static readonly DependencyProperty AutoMoveFocusProperty = DependencyProperty.Register("AutoMoveFocus", typeof(bool), typeof(NumericUpDown<T>), new UIPropertyMetadata(false));

	public static readonly DependencyProperty AutoSelectBehaviorProperty = DependencyProperty.Register("AutoSelectBehavior", typeof(AutoSelectBehavior), typeof(NumericUpDown<T>), new UIPropertyMetadata(AutoSelectBehavior.OnFocus));

	public static readonly DependencyProperty FormatStringProperty = DependencyProperty.Register("FormatString", typeof(string), typeof(NumericUpDown<T>), new UIPropertyMetadata(string.Empty, OnFormatStringChanged));

	public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register("Increment", typeof(T), typeof(NumericUpDown<T>), new PropertyMetadata(default(T), OnIncrementChanged, OnCoerceIncrement));

	public bool AutoMoveFocus
	{
		get
		{
			return (bool)GetValue(AutoMoveFocusProperty);
		}
		set
		{
			SetValue(AutoMoveFocusProperty, value);
		}
	}

	public AutoSelectBehavior AutoSelectBehavior
	{
		get
		{
			return (AutoSelectBehavior)GetValue(AutoSelectBehaviorProperty);
		}
		set
		{
			SetValue(AutoSelectBehaviorProperty, value);
		}
	}

	public string FormatString
	{
		get
		{
			return (string)GetValue(FormatStringProperty);
		}
		set
		{
			SetValue(FormatStringProperty, value);
		}
	}

	public T Increment
	{
		get
		{
			return (T)GetValue(IncrementProperty);
		}
		set
		{
			SetValue(IncrementProperty, value);
		}
	}

	private static void OnFormatStringChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is NumericUpDown<T> numericUpDown)
		{
			numericUpDown.OnFormatStringChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnFormatStringChanged(string oldValue, string newValue)
	{
		if (IsInitialized)
		{
			SyncTextAndValueProperties(updateValueFromText: false, null);
		}
	}

	private static void OnIncrementChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is NumericUpDown<T> numericUpDown)
		{
			numericUpDown.OnIncrementChanged((T)e.OldValue, (T)e.NewValue);
		}
	}

	protected virtual void OnIncrementChanged(T oldValue, T newValue)
	{
		if (IsInitialized)
		{
			SetValidSpinDirection();
		}
	}

	private static object OnCoerceIncrement(DependencyObject d, object baseValue)
	{
		if (d is NumericUpDown<T> numericUpDown)
		{
			return numericUpDown.OnCoerceIncrement((T)baseValue);
		}
		return baseValue;
	}

	protected virtual T OnCoerceIncrement(T baseValue)
	{
		return baseValue;
	}

	protected static decimal ParsePercent(string text, IFormatProvider cultureInfo)
	{
		NumberFormatInfo instance = NumberFormatInfo.GetInstance(cultureInfo);
		text = text.Replace(instance.PercentSymbol, null);
		return decimal.Parse(text, NumberStyles.Any, instance) / 100m;
	}
}
