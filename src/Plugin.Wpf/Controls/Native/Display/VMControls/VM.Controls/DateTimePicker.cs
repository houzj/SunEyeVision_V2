using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_Calendar", Type = typeof(Calendar))]
[TemplatePart(Name = "PART_TimeUpDown", Type = typeof(TimePicker))]
public class DateTimePicker : DateTimePickerBase
{
	private const string PART_Calendar = "PART_Calendar";

	private const string PART_TimeUpDown = "PART_TimeUpDown";

	private Calendar _calendar;

	private TimePicker _timePicker;

	private DateTime? _calendarTemporaryDateTime;

	private DateTime? _calendarIntendedDateTime;

	public static readonly DependencyProperty AutoCloseCalendarProperty;

	public static readonly DependencyProperty CalendarDisplayModeProperty;

	public static readonly DependencyProperty CalendarWidthProperty;

	public static readonly DependencyProperty TimeFormatProperty;

	public static readonly DependencyProperty TimeFormatStringProperty;

	public static readonly DependencyProperty TimePickerAllowSpinProperty;

	public static readonly DependencyProperty TimePickerShowButtonSpinnerProperty;

	public static readonly DependencyProperty TimePickerVisibilityProperty;

	public static readonly DependencyProperty TimeWatermarkProperty;

	public static readonly DependencyProperty TimeWatermarkTemplateProperty;

	public bool AutoCloseCalendar
	{
		get
		{
			return (bool)GetValue(AutoCloseCalendarProperty);
		}
		set
		{
			SetValue(AutoCloseCalendarProperty, value);
		}
	}

	public CalendarMode CalendarDisplayMode
	{
		get
		{
			return (CalendarMode)GetValue(CalendarDisplayModeProperty);
		}
		set
		{
			SetValue(CalendarDisplayModeProperty, value);
		}
	}

	public double CalendarWidth
	{
		get
		{
			return (double)GetValue(CalendarWidthProperty);
		}
		set
		{
			SetValue(CalendarWidthProperty, value);
		}
	}

	public DateTimeFormat TimeFormat
	{
		get
		{
			return (DateTimeFormat)GetValue(TimeFormatProperty);
		}
		set
		{
			SetValue(TimeFormatProperty, value);
		}
	}

	public string TimeFormatString
	{
		get
		{
			return (string)GetValue(TimeFormatStringProperty);
		}
		set
		{
			SetValue(TimeFormatStringProperty, value);
		}
	}

	public bool TimePickerAllowSpin
	{
		get
		{
			return (bool)GetValue(TimePickerAllowSpinProperty);
		}
		set
		{
			SetValue(TimePickerAllowSpinProperty, value);
		}
	}

	public bool TimePickerShowButtonSpinner
	{
		get
		{
			return (bool)GetValue(TimePickerShowButtonSpinnerProperty);
		}
		set
		{
			SetValue(TimePickerShowButtonSpinnerProperty, value);
		}
	}

	public Visibility TimePickerVisibility
	{
		get
		{
			return (Visibility)GetValue(TimePickerVisibilityProperty);
		}
		set
		{
			SetValue(TimePickerVisibilityProperty, value);
		}
	}

	public object TimeWatermark
	{
		get
		{
			return GetValue(TimeWatermarkProperty);
		}
		set
		{
			SetValue(TimeWatermarkProperty, value);
		}
	}

	public DataTemplate TimeWatermarkTemplate
	{
		get
		{
			return (DataTemplate)GetValue(TimeWatermarkTemplateProperty);
		}
		set
		{
			SetValue(TimeWatermarkTemplateProperty, value);
		}
	}

	private static bool IsTimeFormatStringValid(object value)
	{
		return DateTimeUpDown.IsFormatStringValid(value);
	}

	static DateTimePicker()
	{
		AutoCloseCalendarProperty = DependencyProperty.Register("AutoCloseCalendar", typeof(bool), typeof(DateTimePicker), new UIPropertyMetadata(false));
		CalendarDisplayModeProperty = DependencyProperty.Register("CalendarDisplayMode", typeof(CalendarMode), typeof(DateTimePicker), new UIPropertyMetadata(CalendarMode.Month));
		CalendarWidthProperty = DependencyProperty.Register("CalendarWidth", typeof(double), typeof(DateTimePicker), new UIPropertyMetadata(178.0));
		TimeFormatProperty = DependencyProperty.Register("TimeFormat", typeof(DateTimeFormat), typeof(DateTimePicker), new UIPropertyMetadata(DateTimeFormat.ShortTime));
		TimeFormatStringProperty = DependencyProperty.Register("TimeFormatString", typeof(string), typeof(DateTimePicker), new UIPropertyMetadata((object)null), IsTimeFormatStringValid);
		TimePickerAllowSpinProperty = DependencyProperty.Register("TimePickerAllowSpin", typeof(bool), typeof(DateTimePicker), new UIPropertyMetadata(true));
		TimePickerShowButtonSpinnerProperty = DependencyProperty.Register("TimePickerShowButtonSpinner", typeof(bool), typeof(DateTimePicker), new UIPropertyMetadata(true));
		TimePickerVisibilityProperty = DependencyProperty.Register("TimePickerVisibility", typeof(Visibility), typeof(DateTimePicker), new UIPropertyMetadata(Visibility.Visible));
		TimeWatermarkProperty = DependencyProperty.Register("TimeWatermark", typeof(object), typeof(DateTimePicker), new UIPropertyMetadata(null));
		TimeWatermarkTemplateProperty = DependencyProperty.Register("TimeWatermarkTemplate", typeof(DataTemplate), typeof(DateTimePicker), new UIPropertyMetadata(null));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(DateTimePicker), new FrameworkPropertyMetadata(typeof(DateTimePicker)));
		UpDownBase<DateTime?>.UpdateValueOnEnterKeyProperty.OverrideMetadata(typeof(DateTimePicker), new FrameworkPropertyMetadata(true));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_calendar != null)
		{
			_calendar.SelectedDatesChanged -= Calendar_SelectedDatesChanged;
		}
		_calendar = GetTemplateChild("PART_Calendar") as Calendar;
		if (_calendar != null)
		{
			_calendar.SelectedDatesChanged += Calendar_SelectedDatesChanged;
			_calendar.SelectedDate = Value ?? ((DateTime?)null);
			_calendar.DisplayDate = Value ?? ContextNow;
			SetBlackOutDates();
		}
		if (_timePicker != null)
		{
			_timePicker.ValueChanged -= TimePicker_ValueChanged;
		}
		_timePicker = GetTemplateChild("PART_TimeUpDown") as TimePicker;
		if (_timePicker != null)
		{
			_timePicker.ValueChanged += TimePicker_ValueChanged;
		}
	}

	protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
	{
		if (Mouse.Captured is CalendarItem)
		{
			Mouse.Capture(null);
			if (AutoCloseCalendar && _calendar != null && _calendar.DisplayMode == CalendarMode.Month)
			{
				ClosePopup(isFocusOnTextBox: true);
			}
		}
		base.OnPreviewMouseUp(e);
	}

	protected override void OnValueChanged(DateTime? oldValue, DateTime? newValue)
	{
		DateTime? dateTime = (newValue.HasValue ? new DateTime?(newValue.Value.Date) : ((DateTime?)null));
		if (_calendar != null && _calendar.SelectedDate != dateTime)
		{
			_calendar.SelectedDate = dateTime;
			_calendar.DisplayDate = newValue.GetValueOrDefault(ContextNow);
		}
		if (_calendar != null && _calendarTemporaryDateTime.HasValue && newValue != _calendarTemporaryDateTime)
		{
			_calendarTemporaryDateTime = null;
			_calendarIntendedDateTime = null;
		}
		base.OnValueChanged(oldValue, newValue);
	}

	protected override void OnIsOpenChanged(bool oldValue, bool newValue)
	{
		base.OnIsOpenChanged(oldValue, newValue);
		if (!newValue)
		{
			_calendarTemporaryDateTime = null;
			_calendarIntendedDateTime = null;
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		if (!IsOpen)
		{
			base.OnPreviewKeyDown(e);
		}
	}

	protected override void OnMaximumChanged(DateTime? oldValue, DateTime? newValue)
	{
		base.OnMaximumChanged(oldValue, newValue);
		SetBlackOutDates();
	}

	protected override void OnMinimumChanged(DateTime? oldValue, DateTime? newValue)
	{
		base.OnMinimumChanged(oldValue, newValue);
		SetBlackOutDates();
	}

	protected override void HandleKeyDown(object sender, KeyEventArgs e)
	{
		if (!IsOpen || _timePicker == null || !_timePicker.IsKeyboardFocusWithin || (!_timePicker.IsOpen && !e.Handled))
		{
			base.HandleKeyDown(sender, e);
		}
	}

	private void TimePicker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
	{
		e.Handled = true;
		if (UpdateValueOnEnterKey)
		{
			DateTime? dateTime = e.NewValue as DateTime?;
			if (dateTime.HasValue)
			{
				_fireSelectionChangedEvent = false;
				DateTime dateTime2 = ConvertTextToValue(TextBox.Text) ?? ContextNow;
				DateTime dateTime3 = new DateTime(dateTime2.Year, dateTime2.Month, dateTime2.Day, dateTime.Value.Hour, dateTime.Value.Minute, dateTime.Value.Second, dateTime.Value.Millisecond, dateTime2.Kind);
				TextBox.Text = dateTime3.ToString(GetFormatString(Format), CultureInfo);
				_fireSelectionChangedEvent = true;
			}
		}
	}

	private void Calendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count <= 0)
		{
			return;
		}
		DateTime? dateTime = (DateTime?)e.AddedItems[0];
		if (dateTime.HasValue)
		{
			dateTime = DateTime.SpecifyKind(dateTime.Value, Kind);
			if (_calendarIntendedDateTime.HasValue)
			{
				dateTime = dateTime.Value.Date + _calendarIntendedDateTime.Value.TimeOfDay;
				_calendarTemporaryDateTime = null;
				_calendarIntendedDateTime = null;
			}
			else if (Value.HasValue)
			{
				dateTime = dateTime.Value.Date + Value.Value.TimeOfDay;
			}
			DateTime? clippedMinMaxValue = GetClippedMinMaxValue(dateTime);
			if (clippedMinMaxValue.Value != dateTime.Value)
			{
				_calendarTemporaryDateTime = clippedMinMaxValue;
				_calendarIntendedDateTime = dateTime;
				dateTime = clippedMinMaxValue;
			}
		}
		if (UpdateValueOnEnterKey)
		{
			_fireSelectionChangedEvent = false;
			if (dateTime.HasValue)
			{
				TextBox.Text = dateTime.Value.ToString(GetFormatString(Format), CultureInfo);
			}
			_fireSelectionChangedEvent = true;
		}
		else if (!object.Equals(dateTime, Value))
		{
			Value = dateTime;
		}
	}

	protected override void Popup_Opened(object sender, EventArgs e)
	{
		base.Popup_Opened(sender, e);
		if (_calendar != null)
		{
			_calendar.Focus();
		}
	}

	private void SetBlackOutDates()
	{
		if (_calendar != null)
		{
			_calendar.BlackoutDates.Clear();
			if (Minimum.HasValue && Minimum.HasValue && Minimum.Value != DateTime.MinValue)
			{
				DateTime value = Minimum.Value;
				_calendar.BlackoutDates.Add(new CalendarDateRange(DateTime.MinValue, value.AddDays(-1.0)));
			}
			if (Maximum.HasValue && Maximum.HasValue && Maximum.Value != DateTime.MaxValue)
			{
				DateTime value2 = Maximum.Value;
				_calendar.BlackoutDates.Add(new CalendarDateRange(value2.AddDays(1.0), DateTime.MaxValue));
			}
		}
	}
}
