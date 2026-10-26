using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

public abstract class InputBase : Control
{
	public static readonly DependencyProperty AllowTextInputProperty = DependencyProperty.Register("AllowTextInput", typeof(bool), typeof(InputBase), new UIPropertyMetadata(true, OnAllowTextInputChanged));

	public static readonly DependencyProperty CultureInfoProperty = DependencyProperty.Register("CultureInfo", typeof(CultureInfo), typeof(InputBase), new UIPropertyMetadata(CultureInfo.CurrentCulture, OnCultureInfoChanged));

	public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(InputBase), new UIPropertyMetadata(false, OnReadOnlyChanged));

	public static readonly DependencyProperty IsUndoEnabledProperty = DependencyProperty.Register("IsUndoEnabled", typeof(bool), typeof(InputBase), new UIPropertyMetadata(true, OnIsUndoEnabledChanged));

	public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(InputBase), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged, null, isAnimationProhibited: false, UpdateSourceTrigger.LostFocus));

	public static readonly DependencyProperty TextAlignmentProperty = DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(InputBase), new UIPropertyMetadata(TextAlignment.Left));

	public static readonly DependencyProperty WatermarkProperty = DependencyProperty.Register("Watermark", typeof(object), typeof(InputBase), new UIPropertyMetadata(null));

	public static readonly DependencyProperty WatermarkTemplateProperty = DependencyProperty.Register("WatermarkTemplate", typeof(DataTemplate), typeof(InputBase), new UIPropertyMetadata(null));

	public bool AllowTextInput
	{
		get
		{
			return (bool)GetValue(AllowTextInputProperty);
		}
		set
		{
			SetValue(AllowTextInputProperty, value);
		}
	}

	public CultureInfo CultureInfo
	{
		get
		{
			return (CultureInfo)GetValue(CultureInfoProperty);
		}
		set
		{
			SetValue(CultureInfoProperty, value);
		}
	}

	public bool IsReadOnly
	{
		get
		{
			return (bool)GetValue(IsReadOnlyProperty);
		}
		set
		{
			SetValue(IsReadOnlyProperty, value);
		}
	}

	public bool IsUndoEnabled
	{
		get
		{
			return (bool)GetValue(IsUndoEnabledProperty);
		}
		set
		{
			SetValue(IsUndoEnabledProperty, value);
		}
	}

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	public TextAlignment TextAlignment
	{
		get
		{
			return (TextAlignment)GetValue(TextAlignmentProperty);
		}
		set
		{
			SetValue(TextAlignmentProperty, value);
		}
	}

	public object Watermark
	{
		get
		{
			return GetValue(WatermarkProperty);
		}
		set
		{
			SetValue(WatermarkProperty, value);
		}
	}

	public DataTemplate WatermarkTemplate
	{
		get
		{
			return (DataTemplate)GetValue(WatermarkTemplateProperty);
		}
		set
		{
			SetValue(WatermarkTemplateProperty, value);
		}
	}

	private static void OnAllowTextInputChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is InputBase inputBase)
		{
			inputBase.OnAllowTextInputChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnAllowTextInputChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnCultureInfoChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is InputBase inputBase)
		{
			inputBase.OnCultureInfoChanged((CultureInfo)e.OldValue, (CultureInfo)e.NewValue);
		}
	}

	protected virtual void OnCultureInfoChanged(CultureInfo oldValue, CultureInfo newValue)
	{
	}

	private static void OnReadOnlyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is InputBase inputBase)
		{
			inputBase.OnReadOnlyChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnReadOnlyChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnIsUndoEnabledChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is InputBase inputBase)
		{
			inputBase.OnIsUndoEnabledChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsUndoEnabledChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is InputBase inputBase)
		{
			inputBase.OnTextChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnTextChanged(string oldValue, string newValue)
	{
	}
}
