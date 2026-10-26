using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Input;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

[TemplatePart(Name = "PART_TextBox", Type = typeof(TextBox))]
[TemplatePart(Name = "PART_Spinner", Type = typeof(Spinner))]
public abstract class UpDownBase<T> : InputBase, IValidateInput
{
	internal const string PART_TextBox = "PART_TextBox";

	internal const string PART_Spinner = "PART_Spinner";

	internal bool _isTextChangedFromUI;

	private bool _isSyncingTextAndValueProperties;

	private bool _internalValueSet;

	public static readonly DependencyProperty AllowSpinProperty = DependencyProperty.Register("AllowSpin", typeof(bool), typeof(UpDownBase<T>), new UIPropertyMetadata(true));

	public static readonly DependencyProperty ButtonSpinnerLocationProperty = DependencyProperty.Register("ButtonSpinnerLocation", typeof(Location), typeof(UpDownBase<T>), new UIPropertyMetadata(Location.Right));

	public static readonly DependencyProperty ClipValueToMinMaxProperty = DependencyProperty.Register("ClipValueToMinMax", typeof(bool), typeof(UpDownBase<T>), new UIPropertyMetadata(false));

	public static readonly DependencyProperty DisplayDefaultValueOnEmptyTextProperty = DependencyProperty.Register("DisplayDefaultValueOnEmptyText", typeof(bool), typeof(UpDownBase<T>), new UIPropertyMetadata(false, OnDisplayDefaultValueOnEmptyTextChanged));

	public static readonly DependencyProperty DefaultValueProperty = DependencyProperty.Register("DefaultValue", typeof(T), typeof(UpDownBase<T>), new UIPropertyMetadata(default(T), OnDefaultValueChanged));

	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(T), typeof(UpDownBase<T>), new UIPropertyMetadata(default(T), OnMaximumChanged, OnCoerceMaximum));

	public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(T), typeof(UpDownBase<T>), new UIPropertyMetadata(default(T), OnMinimumChanged, OnCoerceMinimum));

	public static readonly DependencyProperty MouseWheelActiveTriggerProperty = DependencyProperty.Register("MouseWheelActiveTrigger", typeof(MouseWheelActiveTrigger), typeof(UpDownBase<T>), new UIPropertyMetadata(MouseWheelActiveTrigger.FocusedMouseOver));

	[Obsolete("Use MouseWheelActiveTrigger property instead")]
	public static readonly DependencyProperty MouseWheelActiveOnFocusProperty = DependencyProperty.Register("MouseWheelActiveOnFocus", typeof(bool), typeof(UpDownBase<T>), new UIPropertyMetadata(true, OnMouseWheelActiveOnFocusChanged));

	public static readonly DependencyProperty ShowButtonSpinnerProperty = DependencyProperty.Register("ShowButtonSpinner", typeof(bool), typeof(UpDownBase<T>), new UIPropertyMetadata(true));

	public static readonly DependencyProperty UpdateValueOnEnterKeyProperty = DependencyProperty.Register("UpdateValueOnEnterKey", typeof(bool), typeof(UpDownBase<T>), new FrameworkPropertyMetadata(false, OnUpdateValueOnEnterKeyChanged));

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(T), typeof(UpDownBase<T>), new FrameworkPropertyMetadata(default(T), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, OnCoerceValue, isAnimationProhibited: false, UpdateSourceTrigger.PropertyChanged));

	public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent("ValueChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<object>), typeof(UpDownBase<T>));

	protected Spinner Spinner { get; private set; }

	protected TextBox TextBox { get; private set; }

	public bool AllowSpin
	{
		get
		{
			return (bool)GetValue(AllowSpinProperty);
		}
		set
		{
			SetValue(AllowSpinProperty, value);
		}
	}

	public Location ButtonSpinnerLocation
	{
		get
		{
			return (Location)GetValue(ButtonSpinnerLocationProperty);
		}
		set
		{
			SetValue(ButtonSpinnerLocationProperty, value);
		}
	}

	public bool ClipValueToMinMax
	{
		get
		{
			return (bool)GetValue(ClipValueToMinMaxProperty);
		}
		set
		{
			SetValue(ClipValueToMinMaxProperty, value);
		}
	}

	public bool DisplayDefaultValueOnEmptyText
	{
		get
		{
			return (bool)GetValue(DisplayDefaultValueOnEmptyTextProperty);
		}
		set
		{
			SetValue(DisplayDefaultValueOnEmptyTextProperty, value);
		}
	}

	public T DefaultValue
	{
		get
		{
			return (T)GetValue(DefaultValueProperty);
		}
		set
		{
			SetValue(DefaultValueProperty, value);
		}
	}

	public T Maximum
	{
		get
		{
			return (T)GetValue(MaximumProperty);
		}
		set
		{
			SetValue(MaximumProperty, value);
		}
	}

	public T Minimum
	{
		get
		{
			return (T)GetValue(MinimumProperty);
		}
		set
		{
			SetValue(MinimumProperty, value);
		}
	}

	public MouseWheelActiveTrigger MouseWheelActiveTrigger
	{
		get
		{
			return (MouseWheelActiveTrigger)GetValue(MouseWheelActiveTriggerProperty);
		}
		set
		{
			SetValue(MouseWheelActiveTriggerProperty, value);
		}
	}

	[Obsolete("Use MouseWheelActiveTrigger property instead")]
	public bool MouseWheelActiveOnFocus
	{
		get
		{
			return (bool)GetValue(MouseWheelActiveOnFocusProperty);
		}
		set
		{
			SetValue(MouseWheelActiveOnFocusProperty, value);
		}
	}

	public bool ShowButtonSpinner
	{
		get
		{
			return (bool)GetValue(ShowButtonSpinnerProperty);
		}
		set
		{
			SetValue(ShowButtonSpinnerProperty, value);
		}
	}

	public bool UpdateValueOnEnterKey
	{
		get
		{
			return (bool)GetValue(UpdateValueOnEnterKeyProperty);
		}
		set
		{
			SetValue(UpdateValueOnEnterKeyProperty, value);
		}
	}

	public T Value
	{
		get
		{
			return (T)GetValue(ValueProperty);
		}
		set
		{
			SetValue(ValueProperty, value);
		}
	}

	public event InputValidationErrorEventHandler InputValidationError;

	public event EventHandler<SpinEventArgs> Spinned;

	public event RoutedPropertyChangedEventHandler<object> ValueChanged
	{
		add
		{
			AddHandler(ValueChangedEvent, value);
		}
		remove
		{
			RemoveHandler(ValueChangedEvent, value);
		}
	}

	private static void OnDisplayDefaultValueOnEmptyTextChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
	{
		((UpDownBase<T>)source).OnDisplayDefaultValueOnEmptyTextChanged((bool)args.OldValue, (bool)args.NewValue);
	}

	private void OnDisplayDefaultValueOnEmptyTextChanged(bool oldValue, bool newValue)
	{
		if (IsInitialized && string.IsNullOrEmpty(Text))
		{
			SyncTextAndValueProperties(updateValueFromText: false, Text);
		}
	}

	private static void OnDefaultValueChanged(DependencyObject source, DependencyPropertyChangedEventArgs args)
	{
		((UpDownBase<T>)source).OnDefaultValueChanged((T)args.OldValue, (T)args.NewValue);
	}

	private void OnDefaultValueChanged(T oldValue, T newValue)
	{
		if (IsInitialized && string.IsNullOrEmpty(Text))
		{
			SyncTextAndValueProperties(updateValueFromText: true, Text);
		}
	}

	private static void OnMaximumChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is UpDownBase<T> upDownBase)
		{
			upDownBase.OnMaximumChanged((T)e.OldValue, (T)e.NewValue);
		}
	}

	protected virtual void OnMaximumChanged(T oldValue, T newValue)
	{
		if (IsInitialized)
		{
			SetValidSpinDirection();
		}
	}

	private static object OnCoerceMaximum(DependencyObject d, object baseValue)
	{
		if (d is UpDownBase<T> upDownBase)
		{
			return upDownBase.OnCoerceMaximum((T)baseValue);
		}
		return baseValue;
	}

	protected virtual T OnCoerceMaximum(T baseValue)
	{
		return baseValue;
	}

	private static void OnMinimumChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is UpDownBase<T> upDownBase)
		{
			upDownBase.OnMinimumChanged((T)e.OldValue, (T)e.NewValue);
		}
	}

	protected virtual void OnMinimumChanged(T oldValue, T newValue)
	{
		if (IsInitialized)
		{
			SetValidSpinDirection();
		}
	}

	private static object OnCoerceMinimum(DependencyObject d, object baseValue)
	{
		if (d is UpDownBase<T> upDownBase)
		{
			return upDownBase.OnCoerceMinimum((T)baseValue);
		}
		return baseValue;
	}

	protected virtual T OnCoerceMinimum(T baseValue)
	{
		return baseValue;
	}

	private static void OnMouseWheelActiveOnFocusChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is UpDownBase<T> upDownBase)
		{
			upDownBase.MouseWheelActiveTrigger = (((bool)e.NewValue) ? MouseWheelActiveTrigger.FocusedMouseOver : MouseWheelActiveTrigger.MouseOver);
		}
	}

	private static void OnUpdateValueOnEnterKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is UpDownBase<T> upDownBase)
		{
			upDownBase.OnUpdateValueOnEnterKeyChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnUpdateValueOnEnterKeyChanged(bool oldValue, bool newValue)
	{
	}

	private void SetValueInternal(T value)
	{
		_internalValueSet = true;
		try
		{
			Value = value;
		}
		finally
		{
			_internalValueSet = false;
		}
	}

	private static object OnCoerceValue(DependencyObject o, object basevalue)
	{
		return ((UpDownBase<T>)o).OnCoerceValue(basevalue);
	}

	protected virtual object OnCoerceValue(object newValue)
	{
		return newValue;
	}

	private static void OnValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is UpDownBase<T> upDownBase)
		{
			upDownBase.OnValueChanged((T)e.OldValue, (T)e.NewValue);
		}
	}

	protected virtual void OnValueChanged(T oldValue, T newValue)
	{
		if (!_internalValueSet && IsInitialized)
		{
			SyncTextAndValueProperties(updateValueFromText: false, null, forceTextUpdate: true);
		}
		SetValidSpinDirection();
		RaiseValueChangedEvent(oldValue, newValue);
	}

	internal UpDownBase()
	{
		AddHandler(Mouse.PreviewMouseDownOutsideCapturedElementEvent, new RoutedEventHandler(HandleClickOutsideOfControlWithMouseCapture), handledEventsToo: true);
		IsKeyboardFocusWithinChanged += UpDownBase_IsKeyboardFocusWithinChanged;
	}

	protected override void OnAccessKey(AccessKeyEventArgs e)
	{
		if (TextBox != null)
		{
			TextBox.Focus();
		}
		base.OnAccessKey(e);
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (TextBox != null)
		{
			TextBox.TextChanged -= TextBox_TextChanged;
			TextBox.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(TextBox_PreviewMouseDown));
		}
		TextBox = GetTemplateChild("PART_TextBox") as TextBox;
		if (TextBox != null)
		{
			TextBox.Text = Text;
			TextBox.TextChanged += TextBox_TextChanged;
			TextBox.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(TextBox_PreviewMouseDown), handledEventsToo: true);
		}
		if (Spinner != null)
		{
			Spinner.Spin -= OnSpinnerSpin;
		}
		Spinner = GetTemplateChild("PART_Spinner") as Spinner;
		if (Spinner != null)
		{
			Spinner.Spin += OnSpinnerSpin;
		}
		SetValidSpinDirection();
	}

	protected override void OnKeyDown(KeyEventArgs e)
	{
		Key key = e.Key;
		if (key == Key.Return)
		{
			bool flag = CommitInput();
			e.Handled = !flag;
		}
	}

	protected override void OnTextChanged(string oldValue, string newValue)
	{
		if (!IsInitialized)
		{
			return;
		}
		if (UpdateValueOnEnterKey)
		{
			if (!_isTextChangedFromUI)
			{
				SyncTextAndValueProperties(updateValueFromText: true, Text);
			}
		}
		else
		{
			SyncTextAndValueProperties(updateValueFromText: true, Text);
		}
	}

	protected override void OnCultureInfoChanged(CultureInfo oldValue, CultureInfo newValue)
	{
		if (IsInitialized)
		{
			SyncTextAndValueProperties(updateValueFromText: false, null);
		}
	}

	protected override void OnReadOnlyChanged(bool oldValue, bool newValue)
	{
		SetValidSpinDirection();
	}

	private void TextBox_PreviewMouseDown(object sender, RoutedEventArgs e)
	{
		if (MouseWheelActiveTrigger == MouseWheelActiveTrigger.Focused && Mouse.Captured != Spinner)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)(() =>
			{
				Mouse.Capture(Spinner);
			}));
		}
	}

	private void HandleClickOutsideOfControlWithMouseCapture(object sender, RoutedEventArgs e)
	{
		if (Mouse.Captured is Spinner)
		{
			Spinner.ReleaseMouseCapture();
		}
	}

	private void OnSpinnerSpin(object sender, SpinEventArgs e)
	{
		if (AllowSpin && !IsReadOnly)
		{
			MouseWheelActiveTrigger mouseWheelActiveTrigger = MouseWheelActiveTrigger;
			if (!e.UsingMouseWheel | (mouseWheelActiveTrigger == MouseWheelActiveTrigger.MouseOver) | (TextBox != null && TextBox.IsFocused && mouseWheelActiveTrigger == MouseWheelActiveTrigger.FocusedMouseOver) | (TextBox != null && TextBox.IsFocused && mouseWheelActiveTrigger == MouseWheelActiveTrigger.Focused && Mouse.Captured is Spinner))
			{
				e.Handled = true;
				OnSpin(e);
			}
		}
	}

	protected virtual void OnSpin(SpinEventArgs e)
	{
		if (e == null)
		{
			throw new ArgumentNullException("e");
		}
		Spinned?.Invoke(this, e);
		if (e.Direction == SpinDirection.Increase)
		{
			DoIncrement();
		}
		else
		{
			DoDecrement();
		}
	}

	protected virtual void RaiseValueChangedEvent(T oldValue, T newValue)
	{
		RoutedPropertyChangedEventArgs<object> e = new RoutedPropertyChangedEventArgs<object>(oldValue, newValue);
		e.RoutedEvent = ValueChangedEvent;
		RaiseEvent(e);
	}

	protected override void OnInitialized(EventArgs e)
	{
		base.OnInitialized(e);
		bool flag = ReadLocalValue(ValueProperty) == DependencyProperty.UnsetValue && BindingOperations.GetBinding(this, ValueProperty) == null && object.Equals(Value, ValueProperty.DefaultMetadata.DefaultValue);
		SyncTextAndValueProperties(flag, Text, !flag);
	}

	internal void DoDecrement()
	{
		if (Spinner == null || (Spinner.ValidSpinDirection & ValidSpinDirections.Decrease) == ValidSpinDirections.Decrease)
		{
			OnDecrement();
		}
	}

	internal void DoIncrement()
	{
		if (Spinner == null || (Spinner.ValidSpinDirection & ValidSpinDirections.Increase) == ValidSpinDirections.Increase)
		{
			OnIncrement();
		}
	}

	private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		if (!IsKeyboardFocusWithin)
		{
			return;
		}
		try
		{
			_isTextChangedFromUI = true;
			Text = ((TextBox)sender).Text;
		}
		finally
		{
			_isTextChangedFromUI = false;
		}
	}

	private void UpDownBase_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
	{
		if (!(bool)e.NewValue)
		{
			CommitInput();
		}
	}

	private void RaiseInputValidationError(Exception e)
	{
		if (InputValidationError != null)
		{
			InputValidationErrorEventArgs e2 = new InputValidationErrorEventArgs(e);
			InputValidationError(this, e2);
			if (e2.ThrowException)
			{
				throw e2.Exception;
			}
		}
	}

	public virtual bool CommitInput()
	{
		return SyncTextAndValueProperties(updateValueFromText: true, Text);
	}

	protected bool SyncTextAndValueProperties(bool updateValueFromText, string text)
	{
		return SyncTextAndValueProperties(updateValueFromText, text, forceTextUpdate: false);
	}

	private bool SyncTextAndValueProperties(bool updateValueFromText, string text, bool forceTextUpdate)
	{
		if (_isSyncingTextAndValueProperties)
		{
			return true;
		}
		_isSyncingTextAndValueProperties = true;
		bool flag = true;
		try
		{
			if (updateValueFromText)
			{
				if (string.IsNullOrEmpty(text))
				{
					SetValueInternal(DefaultValue);
				}
				else
				{
					try
					{
						T val = ConvertTextToValue(text);
						if (!object.Equals(val, Value))
						{
							SetValueInternal(val);
						}
					}
					catch (Exception e)
					{
						flag = false;
						if (!_isTextChangedFromUI)
						{
							RaiseInputValidationError(e);
						}
					}
				}
			}
			if (!_isTextChangedFromUI)
			{
				if (forceTextUpdate || !string.IsNullOrEmpty(Text) || !object.Equals(Value, DefaultValue) || DisplayDefaultValueOnEmptyText)
				{
					string text2 = ConvertValueToText();
					if (!object.Equals(Text, text2))
					{
						Text = text2;
					}
				}
				if (TextBox != null)
				{
					TextBox.Text = Text;
				}
			}
			if (_isTextChangedFromUI && !flag)
			{
				if (Spinner != null)
				{
					Spinner.ValidSpinDirection = ValidSpinDirections.None;
				}
			}
			else
			{
				SetValidSpinDirection();
			}
		}
		finally
		{
			_isSyncingTextAndValueProperties = false;
		}
		return flag;
	}

	protected abstract T ConvertTextToValue(string text);

	protected abstract string ConvertValueToText();

	protected abstract void OnIncrement();

	protected abstract void OnDecrement();

	protected abstract void SetValidSpinDirection();
}
