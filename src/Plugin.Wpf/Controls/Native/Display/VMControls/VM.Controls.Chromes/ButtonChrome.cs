using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Chromes;

public class ButtonChrome : ContentControl
{
	public static readonly DependencyProperty CornerRadiusProperty;

	public static readonly DependencyProperty InnerCornerRadiusProperty;

	public static readonly DependencyProperty RenderCheckedProperty;

	public static readonly DependencyProperty RenderEnabledProperty;

	public static readonly DependencyProperty RenderFocusedProperty;

	public static readonly DependencyProperty RenderMouseOverProperty;

	public static readonly DependencyProperty RenderNormalProperty;

	public static readonly DependencyProperty RenderPressedProperty;

	public CornerRadius CornerRadius
	{
		get
		{
			return (CornerRadius)GetValue(CornerRadiusProperty);
		}
		set
		{
			SetValue(CornerRadiusProperty, value);
		}
	}

	public CornerRadius InnerCornerRadius
	{
		get
		{
			return (CornerRadius)GetValue(InnerCornerRadiusProperty);
		}
		set
		{
			SetValue(InnerCornerRadiusProperty, value);
		}
	}

	public bool RenderChecked
	{
		get
		{
			return (bool)GetValue(RenderCheckedProperty);
		}
		set
		{
			SetValue(RenderCheckedProperty, value);
		}
	}

	public bool RenderEnabled
	{
		get
		{
			return (bool)GetValue(RenderEnabledProperty);
		}
		set
		{
			SetValue(RenderEnabledProperty, value);
		}
	}

	public bool RenderFocused
	{
		get
		{
			return (bool)GetValue(RenderFocusedProperty);
		}
		set
		{
			SetValue(RenderFocusedProperty, value);
		}
	}

	public bool RenderMouseOver
	{
		get
		{
			return (bool)GetValue(RenderMouseOverProperty);
		}
		set
		{
			SetValue(RenderMouseOverProperty, value);
		}
	}

	public bool RenderNormal
	{
		get
		{
			return (bool)GetValue(RenderNormalProperty);
		}
		set
		{
			SetValue(RenderNormalProperty, value);
		}
	}

	public bool RenderPressed
	{
		get
		{
			return (bool)GetValue(RenderPressedProperty);
		}
		set
		{
			SetValue(RenderPressedProperty, value);
		}
	}

	private static void OnCornerRadiusChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnCornerRadiusChanged((CornerRadius)e.OldValue, (CornerRadius)e.NewValue);
		}
	}

	protected virtual void OnCornerRadiusChanged(CornerRadius oldValue, CornerRadius newValue)
	{
		CornerRadius innerCornerRadius = new CornerRadius(Math.Max(0.0, newValue.TopLeft - 1.0), Math.Max(0.0, newValue.TopRight - 1.0), Math.Max(0.0, newValue.BottomRight - 1.0), Math.Max(0.0, newValue.BottomLeft - 1.0));
		InnerCornerRadius = innerCornerRadius;
	}

	private static void OnInnerCornerRadiusChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnInnerCornerRadiusChanged((CornerRadius)e.OldValue, (CornerRadius)e.NewValue);
		}
	}

	protected virtual void OnInnerCornerRadiusChanged(CornerRadius oldValue, CornerRadius newValue)
	{
	}

	private static void OnRenderCheckedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnRenderCheckedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnRenderCheckedChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnRenderEnabledChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnRenderEnabledChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnRenderEnabledChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnRenderFocusedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnRenderFocusedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnRenderFocusedChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnRenderMouseOverChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnRenderMouseOverChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnRenderMouseOverChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnRenderNormalChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnRenderNormalChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnRenderNormalChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnRenderPressedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is ButtonChrome buttonChrome)
		{
			buttonChrome.OnRenderPressedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnRenderPressedChanged(bool oldValue, bool newValue)
	{
	}

	static ButtonChrome()
	{
		CornerRadiusProperty = DependencyProperty.Register("CornerRadius", typeof(CornerRadius), typeof(ButtonChrome), new UIPropertyMetadata(default(CornerRadius), OnCornerRadiusChanged));
		InnerCornerRadiusProperty = DependencyProperty.Register("InnerCornerRadius", typeof(CornerRadius), typeof(ButtonChrome), new UIPropertyMetadata(default(CornerRadius), OnInnerCornerRadiusChanged));
		RenderCheckedProperty = DependencyProperty.Register("RenderChecked", typeof(bool), typeof(ButtonChrome), new UIPropertyMetadata(false, OnRenderCheckedChanged));
		RenderEnabledProperty = DependencyProperty.Register("RenderEnabled", typeof(bool), typeof(ButtonChrome), new UIPropertyMetadata(true, OnRenderEnabledChanged));
		RenderFocusedProperty = DependencyProperty.Register("RenderFocused", typeof(bool), typeof(ButtonChrome), new UIPropertyMetadata(false, OnRenderFocusedChanged));
		RenderMouseOverProperty = DependencyProperty.Register("RenderMouseOver", typeof(bool), typeof(ButtonChrome), new UIPropertyMetadata(false, OnRenderMouseOverChanged));
		RenderNormalProperty = DependencyProperty.Register("RenderNormal", typeof(bool), typeof(ButtonChrome), new UIPropertyMetadata(true, OnRenderNormalChanged));
		RenderPressedProperty = DependencyProperty.Register("RenderPressed", typeof(bool), typeof(ButtonChrome), new UIPropertyMetadata(false, OnRenderPressedChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonChrome), new FrameworkPropertyMetadata(typeof(ButtonChrome)));
	}
}
