using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
public class DateTimePickerBase : DateTimeUpDown
{
	private const string PART_Popup = "PART_Popup";

	private Popup _popup;

	private DateTime? _initialValue;

	public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register("IsOpen", typeof(bool), typeof(DateTimePickerBase), new UIPropertyMetadata(false, OnIsOpenChanged));

	public static readonly DependencyProperty ShowDropDownButtonProperty = DependencyProperty.Register("ShowDropDownButton", typeof(bool), typeof(DateTimePickerBase), new UIPropertyMetadata(true));

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

	public bool ShowDropDownButton
	{
		get
		{
			return (bool)GetValue(ShowDropDownButtonProperty);
		}
		set
		{
			SetValue(ShowDropDownButtonProperty, value);
		}
	}

	private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		((DateTimePickerBase)d)?.OnIsOpenChanged((bool)e.OldValue, (bool)e.NewValue);
	}

	protected virtual void OnIsOpenChanged(bool oldValue, bool newValue)
	{
		if (newValue)
		{
			_initialValue = Value;
		}
	}

	public DateTimePickerBase()
	{
		AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(HandleKeyDown), handledEventsToo: true);
		Mouse.AddPreviewMouseDownOutsideCapturedElementHandler(this, OnMouseDownOutsideCapturedElement);
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_popup != null)
		{
			_popup.Opened -= Popup_Opened;
		}
		_popup = GetTemplateChild("PART_Popup") as Popup;
		if (_popup != null)
		{
			_popup.Opened += Popup_Opened;
		}
	}

	protected virtual void HandleKeyDown(object sender, KeyEventArgs e)
	{
		if (!IsOpen)
		{
			if (KeyboardUtilities.IsKeyModifyingPopupState(e))
			{
				IsOpen = true;
				e.Handled = true;
			}
		}
		else if (KeyboardUtilities.IsKeyModifyingPopupState(e))
		{
			ClosePopup(isFocusOnTextBox: true);
			e.Handled = true;
		}
		else if (e.Key == Key.Return)
		{
			ClosePopup(isFocusOnTextBox: true);
			e.Handled = true;
		}
		else if (e.Key == Key.Escape)
		{
			if (!object.Equals(Value, _initialValue))
			{
				Value = _initialValue;
			}
			ClosePopup(isFocusOnTextBox: true);
			e.Handled = true;
		}
	}

	private void OnMouseDownOutsideCapturedElement(object sender, MouseButtonEventArgs e)
	{
		ClosePopup(isFocusOnTextBox: true);
	}

	protected virtual void Popup_Opened(object sender, EventArgs e)
	{
	}

	protected void ClosePopup(bool isFocusOnTextBox)
	{
		if (IsOpen)
		{
			IsOpen = false;
		}
		ReleaseMouseCapture();
		if (isFocusOnTextBox && TextBox != null)
		{
			TextBox.Focus();
		}
	}
}
