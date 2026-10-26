using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_ResizeThumb", Type = typeof(Thumb))]
[TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_DropDownButton", Type = typeof(ToggleButton))]
public class MultiLineTextEditor : ContentControl
{
	private const string PART_ResizeThumb = "PART_ResizeThumb";

	private const string PART_TextBox = "PART_TextBox";

	private const string PART_DropDownButton = "PART_DropDownButton";

	private Thumb _resizeThumb;

	private TextBox _textBox;

	private ToggleButton _toggleButton;

	public static readonly DependencyProperty DropDownHeightProperty;

	public static readonly DependencyProperty DropDownWidthProperty;

	public static readonly DependencyProperty IsOpenProperty;

	public static readonly DependencyProperty IsSpellCheckEnabledProperty;

	public static readonly DependencyProperty IsReadOnlyProperty;

	public static readonly DependencyProperty TextProperty;

	public static readonly DependencyProperty TextAlignmentProperty;

	public static readonly DependencyProperty TextWrappingProperty;

	public double DropDownHeight
	{
		get
		{
			return (double)GetValue(DropDownHeightProperty);
		}
		set
		{
			SetValue(DropDownHeightProperty, value);
		}
	}

	public double DropDownWidth
	{
		get
		{
			return (double)GetValue(DropDownWidthProperty);
		}
		set
		{
			SetValue(DropDownWidthProperty, value);
		}
	}

	public bool IsOpen
	{
		get
		{
			return (bool)GetValue(IsOpenProperty);
		}
		set
		{
			SetValue(IsOpenProperty, value);
		}
	}

	public bool IsSpellCheckEnabled
	{
		get
		{
			return (bool)GetValue(IsSpellCheckEnabledProperty);
		}
		set
		{
			SetValue(IsSpellCheckEnabledProperty, value);
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

	public TextWrapping TextWrapping
	{
		get
		{
			return (TextWrapping)GetValue(TextWrappingProperty);
		}
		set
		{
			SetValue(TextWrappingProperty, value);
		}
	}

	private static void OnIsOpenChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is MultiLineTextEditor multiLineTextEditor)
		{
			multiLineTextEditor.OnIsOpenChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsOpenChanged(bool oldValue, bool newValue)
	{
		if (_textBox != null)
		{
			Dispatcher.BeginInvoke((Action)(() =>
			{
				_textBox.Focus();
			}), DispatcherPriority.Background);
		}
	}

	private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is MultiLineTextEditor multiLineTextEditor)
		{
			multiLineTextEditor.OnTextChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnTextChanged(string oldValue, string newValue)
	{
	}

	static MultiLineTextEditor()
	{
		DropDownHeightProperty = DependencyProperty.Register("DropDownHeight", typeof(double), typeof(MultiLineTextEditor), new UIPropertyMetadata(150.0));
		DropDownWidthProperty = DependencyProperty.Register("DropDownWidth", typeof(double), typeof(MultiLineTextEditor), new UIPropertyMetadata(200.0));
		IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(MultiLineTextEditor), new UIPropertyMetadata(false, OnIsOpenChanged));
		IsSpellCheckEnabledProperty = DependencyProperty.Register("IsSpellCheckEnabled", typeof(bool), typeof(MultiLineTextEditor), new UIPropertyMetadata(false));
		IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(MultiLineTextEditor), new UIPropertyMetadata(false));
		TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(MultiLineTextEditor), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChanged));
		TextAlignmentProperty = DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(MultiLineTextEditor), new UIPropertyMetadata(TextAlignment.Left));
		TextWrappingProperty = DependencyProperty.Register("TextWrapping", typeof(TextWrapping), typeof(MultiLineTextEditor), new UIPropertyMetadata(TextWrapping.NoWrap));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(MultiLineTextEditor), new FrameworkPropertyMetadata(typeof(MultiLineTextEditor)));
	}

	public MultiLineTextEditor()
	{
		Keyboard.AddKeyDownHandler(this, OnKeyDown);
		Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(this, OnMouseDownOutsideCapturedElement);
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_resizeThumb != null)
		{
			_resizeThumb.DragDelta -= ResizeThumb_DragDelta;
		}
		_resizeThumb = GetTemplateChild("PART_ResizeThumb") as Thumb;
		if (_resizeThumb != null)
		{
			_resizeThumb.DragDelta += ResizeThumb_DragDelta;
		}
		_textBox = GetTemplateChild("PART_TextBox") as TextBox;
		_toggleButton = GetTemplateChild("PART_DropDownButton") as ToggleButton;
	}

	private void OnKeyDown(object sender, KeyEventArgs e)
	{
		if (!IsOpen)
		{
			if (KeyboardUtilities.IsKeyModifyingPopupState(e))
			{
				IsOpen = true;
				e.Handled = true;
			}
		}
		else if (KeyboardUtilities.IsKeyModifyingPopupState(e) || e.Key == Key.Escape || e.Key == Key.Tab)
		{
			CloseEditor();
			e.Handled = true;
		}
	}

	private void OnMouseDownOutsideCapturedElement(object sender, MouseButtonEventArgs e)
	{
		CloseEditor();
	}

	private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
	{
		double num = DropDownHeight + e.VerticalChange;
		double num2 = DropDownWidth + e.HorizontalChange;
		if (num2 >= 0.0 && num >= 0.0)
		{
			DropDownWidth = num2;
			DropDownHeight = num;
		}
	}

	private void CloseEditor()
	{
		if (IsOpen)
		{
			IsOpen = false;
		}
		ReleaseMouseCapture();
		if (_toggleButton != null)
		{
			_toggleButton.Focus();
		}
	}
}
