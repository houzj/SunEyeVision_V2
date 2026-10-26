using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_IncreaseButton", Type = typeof(ButtonBase))]
[TemplatePart(Name = "PART_DecreaseButton", Type = typeof(ButtonBase))]
[ContentProperty("Content")]
public class ButtonSpinner : Spinner
{
	private const string PART_IncreaseButton = "PART_IncreaseButton";

	private const string PART_DecreaseButton = "PART_DecreaseButton";

	public static readonly DependencyProperty AllowSpinProperty;

	public static readonly DependencyProperty ButtonSpinnerLocationProperty;

	public static readonly DependencyProperty ContentProperty;

	private ButtonBase _decreaseButton;

	private ButtonBase _increaseButton;

	public static readonly DependencyProperty ShowButtonSpinnerProperty;

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

	public object Content
	{
		get
		{
			return GetValue(ContentProperty);
		}
		set
		{
			SetValue(ContentProperty, value);
		}
	}

	private ButtonBase DecreaseButton
	{
		get
		{
			return _decreaseButton;
		}
		set
		{
			if (_decreaseButton != null)
			{
				_decreaseButton.Click -= OnButtonClick;
			}
			_decreaseButton = value;
			if (_decreaseButton != null)
			{
				_decreaseButton.Click += OnButtonClick;
			}
		}
	}

	private ButtonBase IncreaseButton
	{
		get
		{
			return _increaseButton;
		}
		set
		{
			if (_increaseButton != null)
			{
				_increaseButton.Click -= OnButtonClick;
			}
			_increaseButton = value;
			if (_increaseButton != null)
			{
				_increaseButton.Click += OnButtonClick;
			}
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

	private static void AllowSpinPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as ButtonSpinner).OnAllowSpinChanged((bool)e.OldValue, (bool)e.NewValue);
	}

	private static void OnContentPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as ButtonSpinner).OnContentChanged(e.OldValue, e.NewValue);
	}

	static ButtonSpinner()
	{
		AllowSpinProperty = DependencyProperty.Register("AllowSpin", typeof(bool), typeof(ButtonSpinner), new UIPropertyMetadata(true, AllowSpinPropertyChanged));
		ButtonSpinnerLocationProperty = DependencyProperty.Register("ButtonSpinnerLocation", typeof(Location), typeof(ButtonSpinner), new UIPropertyMetadata(Location.Right));
		ContentProperty = DependencyProperty.Register("Content", typeof(object), typeof(ButtonSpinner), new PropertyMetadata(null, OnContentPropertyChanged));
		ShowButtonSpinnerProperty = DependencyProperty.Register("ShowButtonSpinner", typeof(bool), typeof(ButtonSpinner), new UIPropertyMetadata(true));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(ButtonSpinner), new FrameworkPropertyMetadata(typeof(ButtonSpinner)));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		IncreaseButton = GetTemplateChild("PART_IncreaseButton") as ButtonBase;
		DecreaseButton = GetTemplateChild("PART_DecreaseButton") as ButtonBase;
		SetButtonUsage();
	}

	protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
	{
		base.OnMouseLeftButtonUp(e);
		if (IncreaseButton != null && !IncreaseButton.IsEnabled)
		{
			Point position = e.GetPosition(IncreaseButton);
			if (position.X > 0.0 && position.X < IncreaseButton.ActualWidth && position.Y > 0.0 && position.Y < IncreaseButton.ActualHeight)
			{
				e.Handled = true;
			}
		}
		if (DecreaseButton != null && !DecreaseButton.IsEnabled)
		{
			Point position = e.GetPosition(DecreaseButton);
			if (position.X > 0.0 && position.X < DecreaseButton.ActualWidth && position.Y > 0.0 && position.Y < DecreaseButton.ActualHeight)
			{
				e.Handled = true;
			}
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		switch (e.Key)
		{
		case Key.Up:
			if (AllowSpin)
			{
				OnSpin(new SpinEventArgs(Spinner.SpinnerSpinEvent, SpinDirection.Increase));
				e.Handled = true;
			}
			break;
		case Key.Down:
			if (AllowSpin)
			{
				OnSpin(new SpinEventArgs(Spinner.SpinnerSpinEvent, SpinDirection.Decrease));
				e.Handled = true;
			}
			break;
		case Key.Return:
			if ((IncreaseButton != null && IncreaseButton.IsFocused) || (DecreaseButton != null && DecreaseButton.IsFocused))
			{
				e.Handled = true;
			}
			break;
		}
	}

	protected override void OnMouseWheel(MouseWheelEventArgs e)
	{
		base.OnMouseWheel(e);
		if (!e.Handled && AllowSpin && e.Delta != 0)
		{
			SpinEventArgs e2 = new SpinEventArgs(Spinner.SpinnerSpinEvent, (e.Delta < 0) ? SpinDirection.Decrease : SpinDirection.Increase, usingMouseWheel: true);
			OnSpin(e2);
			e.Handled = e2.Handled;
		}
	}

	protected override void OnValidSpinDirectionChanged(ValidSpinDirections oldValue, ValidSpinDirections newValue)
	{
		SetButtonUsage();
	}

	private void OnButtonClick(object sender, RoutedEventArgs e)
	{
		if (AllowSpin)
		{
			SpinDirection direction = ((sender != IncreaseButton) ? SpinDirection.Decrease : SpinDirection.Increase);
			OnSpin(new SpinEventArgs(Spinner.SpinnerSpinEvent, direction));
		}
	}

	protected virtual void OnContentChanged(object oldValue, object newValue)
	{
	}

	protected virtual void OnAllowSpinChanged(bool oldValue, bool newValue)
	{
		SetButtonUsage();
	}

	private void SetButtonUsage()
	{
		if (IncreaseButton != null)
		{
			IncreaseButton.IsEnabled = AllowSpin && (ValidSpinDirection & ValidSpinDirections.Increase) == ValidSpinDirections.Increase;
		}
		if (DecreaseButton != null)
		{
			DecreaseButton.IsEnabled = AllowSpin && (ValidSpinDirection & ValidSpinDirections.Decrease) == ValidSpinDirections.Decrease;
		}
	}
}
