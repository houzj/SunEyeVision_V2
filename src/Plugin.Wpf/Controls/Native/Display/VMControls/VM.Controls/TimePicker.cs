using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

[TemplatePart(Name = "PART_TimeListItems", Type = typeof(ListBox))]
public class TimePicker : DateTimePickerBase
{
	private const string PART_TimeListItems = "PART_TimeListItems";

	private ListBox _timeListBox;

	private bool _isListBoxInvalid = true;

	internal static readonly TimeSpan EndTimeDefaultValue;

	internal static readonly TimeSpan StartTimeDefaultValue;

	internal static readonly TimeSpan TimeIntervalDefaultValue;

	public static readonly DependencyProperty EndTimeProperty;

	public static readonly DependencyProperty MaxDropDownHeightProperty;

	public static readonly DependencyProperty StartTimeProperty;

	public static readonly DependencyProperty TimeIntervalProperty;

	public TimeSpan EndTime
	{
		get
		{
			return (TimeSpan)GetValue(EndTimeProperty);
		}
		set
		{
			SetValue(EndTimeProperty, value);
		}
	}

	public double MaxDropDownHeight
	{
		get
		{
			return (double)GetValue(MaxDropDownHeightProperty);
		}
		set
		{
			SetValue(MaxDropDownHeightProperty, value);
		}
	}

	public TimeSpan StartTime
	{
		get
		{
			return (TimeSpan)GetValue(StartTimeProperty);
		}
		set
		{
			SetValue(StartTimeProperty, value);
		}
	}

	public TimeSpan TimeInterval
	{
		get
		{
			return (TimeSpan)GetValue(TimeIntervalProperty);
		}
		set
		{
			SetValue(TimeIntervalProperty, value);
		}
	}

	private static object OnCoerceEndTime(DependencyObject o, object value)
	{
		if (o is TimePicker timePicker)
		{
			return timePicker.OnCoerceEndTime((TimeSpan)value);
		}
		return value;
	}

	private static void OnEndTimeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is TimePicker timePicker)
		{
			timePicker.OnEndTimeChanged((TimeSpan)e.OldValue, (TimeSpan)e.NewValue);
		}
	}

	protected virtual TimeSpan OnCoerceEndTime(TimeSpan value)
	{
		ValidateTime(value);
		return value;
	}

	protected virtual void OnEndTimeChanged(TimeSpan oldValue, TimeSpan newValue)
	{
		InvalidateListBoxItems();
	}

	protected override void OnFormatChanged(DateTimeFormat oldValue, DateTimeFormat newValue)
	{
		base.OnFormatChanged(oldValue, newValue);
		InvalidateListBoxItems();
	}

	private static void OnMaxDropDownHeightChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is TimePicker timePicker)
		{
			timePicker.OnMaxDropDownHeightChanged((double)e.OldValue, (double)e.NewValue);
		}
	}

	protected virtual void OnMaxDropDownHeightChanged(double oldValue, double newValue)
	{
	}

	private static object OnCoerceStartTime(DependencyObject o, object value)
	{
		if (o is TimePicker timePicker)
		{
			return timePicker.OnCoerceStartTime((TimeSpan)value);
		}
		return value;
	}

	private static void OnStartTimeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is TimePicker timePicker)
		{
			timePicker.OnStartTimeChanged((TimeSpan)e.OldValue, (TimeSpan)e.NewValue);
		}
	}

	protected virtual TimeSpan OnCoerceStartTime(TimeSpan value)
	{
		ValidateTime(value);
		return value;
	}

	protected virtual void OnStartTimeChanged(TimeSpan oldValue, TimeSpan newValue)
	{
		InvalidateListBoxItems();
	}

	private static object OnCoerceTimeInterval(DependencyObject o, object value)
	{
		if (o is TimePicker timePicker)
		{
			return timePicker.OnCoerceTimeInterval((TimeSpan)value);
		}
		return value;
	}

	private static void OnTimeIntervalChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is TimePicker timePicker)
		{
			timePicker.OnTimeIntervalChanged((TimeSpan)e.OldValue, (TimeSpan)e.NewValue);
		}
	}

	protected virtual TimeSpan OnCoerceTimeInterval(TimeSpan value)
	{
		ValidateTime(value);
		if (value.Ticks == 0L)
		{
			throw new ArgumentException("TimeInterval must be greater than zero");
		}
		return value;
	}

	protected virtual void OnTimeIntervalChanged(TimeSpan oldValue, TimeSpan newValue)
	{
		InvalidateListBoxItems();
	}

	static TimePicker()
	{
		EndTimeDefaultValue = new TimeSpan(23, 59, 0);
		StartTimeDefaultValue = new TimeSpan(0, 0, 0);
		TimeIntervalDefaultValue = new TimeSpan(1, 0, 0);
		EndTimeProperty = DependencyProperty.Register("EndTime", typeof(TimeSpan), typeof(TimePicker), new UIPropertyMetadata(EndTimeDefaultValue, OnEndTimeChanged, OnCoerceEndTime));
		MaxDropDownHeightProperty = DependencyProperty.Register("MaxDropDownHeight", typeof(double), typeof(TimePicker), new UIPropertyMetadata(130.0, OnMaxDropDownHeightChanged));
		StartTimeProperty = DependencyProperty.Register("StartTime", typeof(TimeSpan), typeof(TimePicker), new UIPropertyMetadata(StartTimeDefaultValue, OnStartTimeChanged, OnCoerceStartTime));
		TimeIntervalProperty = DependencyProperty.Register("TimeInterval", typeof(TimeSpan), typeof(TimePicker), new UIPropertyMetadata(TimeIntervalDefaultValue, OnTimeIntervalChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(typeof(TimePicker)));
		DateTimeUpDown.FormatProperty.OverrideMetadata(typeof(TimePicker), new UIPropertyMetadata(DateTimeFormat.ShortTime));
		UpDownBase<DateTime?>.UpdateValueOnEnterKeyProperty.OverrideMetadata(typeof(TimePicker), new FrameworkPropertyMetadata(true));
	}

	protected override void OnFormatStringChanged(string oldValue, string newValue)
	{
		if (Format == DateTimeFormat.Custom)
		{
			InvalidateListBoxItems();
		}
		base.OnFormatStringChanged(oldValue, newValue);
	}

	protected override void OnMaximumChanged(DateTime? oldValue, DateTime? newValue)
	{
		base.OnMaximumChanged(oldValue, newValue);
		InvalidateListBoxItems();
	}

	protected override void OnMinimumChanged(DateTime? oldValue, DateTime? newValue)
	{
		base.OnMinimumChanged(oldValue, newValue);
		InvalidateListBoxItems();
	}

	protected override void OnValueChanged(DateTime? oldValue, DateTime? newValue)
	{
		base.OnValueChanged(oldValue, newValue);
		bool flag = false;
		if (DateTimeUtilities.IsSameDate(Minimum, oldValue) != DateTimeUtilities.IsSameDate(Minimum, newValue))
		{
			flag = true;
		}
		if (DateTimeUtilities.IsSameDate(Maximum, oldValue) != DateTimeUtilities.IsSameDate(Maximum, newValue))
		{
			flag = true;
		}
		if (oldValue.GetValueOrDefault().Date != newValue.GetValueOrDefault().Date)
		{
			flag = true;
		}
		if (flag)
		{
			InvalidateListBoxItems();
		}
		else
		{
			UpdateListBoxSelectedItem();
		}
	}

	protected override void Popup_Opened(object sender, EventArgs e)
	{
		base.Popup_Opened(sender, e);
		if (_timeListBox != null)
		{
			UpdateListBoxItems();
			TimeSpan time = (Value.HasValue ? Value.Value.TimeOfDay : StartTimeDefaultValue);
			TimeItem nearestTimeItem = GetNearestTimeItem(time);
			if (nearestTimeItem != null)
			{
				_timeListBox.ScrollIntoView(nearestTimeItem);
				UpdateListBoxSelectedItem();
			}
			_timeListBox.Focus();
		}
	}

	public override void OnApplyTemplate()
	{
		if (TextBox != null)
		{
			TextBox.GotKeyboardFocus -= TextBoxSpinner_GotKeyboardFocus;
		}
		if (Spinner != null)
		{
			Spinner.GotKeyboardFocus -= TextBoxSpinner_GotKeyboardFocus;
		}
		base.OnApplyTemplate();
		if (TextBox != null)
		{
			TextBox.GotKeyboardFocus += TextBoxSpinner_GotKeyboardFocus;
		}
		if (Spinner != null)
		{
			Spinner.GotKeyboardFocus += TextBoxSpinner_GotKeyboardFocus;
		}
		if (_timeListBox != null)
		{
			_timeListBox.SelectionChanged -= TimeListBox_SelectionChanged;
			_timeListBox.MouseUp -= TimeListBox_MouseUp;
		}
		_timeListBox = GetTemplateChild("PART_TimeListItems") as ListBox;
		if (_timeListBox != null)
		{
			_timeListBox.SelectionChanged += TimeListBox_SelectionChanged;
			_timeListBox.MouseUp += TimeListBox_MouseUp;
			InvalidateListBoxItems();
		}
	}

	private void TimeListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (e.AddedItems.Count > 0)
		{
			TimeSpan time = ((TimeItem)e.AddedItems[0]).Time;
			if (UpdateValueOnEnterKey)
			{
				DateTime dateTime = ConvertTextToValue(TextBox.Text) ?? ContextNow;
				DateTime dateTime2 = new DateTime(dateTime.Year, dateTime.Month, dateTime.Day, time.Hours, time.Minutes, time.Seconds, time.Milliseconds, dateTime.Kind);
				TextBox.Text = dateTime2.ToString(GetFormatString(Format), CultureInfo);
			}
			else
			{
				DateTime dateTime3 = Value ?? ContextNow;
				Value = new DateTime(dateTime3.Year, dateTime3.Month, dateTime3.Day, time.Hours, time.Minutes, time.Seconds, time.Milliseconds, dateTime3.Kind);
			}
		}
	}

	private void TextBoxSpinner_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
	{
		ClosePopup(isFocusOnTextBox: true);
	}

	private void TimeListBox_MouseUp(object sender, MouseButtonEventArgs e)
	{
		ClosePopup(isFocusOnTextBox: true);
	}

	private void ValidateTime(TimeSpan time)
	{
		if (time.TotalHours >= 24.0)
		{
			throw new ArgumentException("Time value cannot be greater than or equal to 24 hours.");
		}
	}

	public IEnumerable GenerateTimeListItemsSource()
	{
		TimeSpan timeSpan = StartTime;
		TimeSpan timeSpan2 = EndTime;
		if (timeSpan2 <= timeSpan)
		{
			timeSpan2 = EndTimeDefaultValue;
			timeSpan = StartTimeDefaultValue;
		}
		if (Value.HasValue)
		{
			DateTime value = Value.Value;
			DateTime valueOrDefault = Minimum.GetValueOrDefault(DateTime.MinValue);
			DateTime valueOrDefault2 = Maximum.GetValueOrDefault(DateTime.MaxValue);
			TimeSpan timeOfDay = valueOrDefault.TimeOfDay;
			TimeSpan timeOfDay2 = valueOrDefault2.TimeOfDay;
			if (value.Date == valueOrDefault.Date && timeSpan.Ticks < timeOfDay.Ticks)
			{
				timeSpan = timeOfDay;
			}
			if (value.Date == valueOrDefault2.Date && timeSpan2.Ticks > timeOfDay2.Ticks)
			{
				timeSpan2 = timeOfDay2;
			}
			if (timeSpan2 < timeSpan)
			{
				timeSpan = timeSpan2;
			}
		}
		TimeSpan timeInterval = TimeInterval;
		List<TimeItem> list = new List<TimeItem>();
		if (timeInterval.Ticks > 0)
		{
			while (timeSpan <= timeSpan2)
			{
				list.Add(CreateTimeItem(timeSpan));
				timeSpan = timeSpan.Add(timeInterval);
			}
		}
		return list;
	}

	protected virtual TimeItem CreateTimeItem(TimeSpan time)
	{
		DateTime dateTime = Value ?? ContextNow;
		string formatString = GetFormatString(Format);
		return new TimeItem(dateTime.Date.Add(time).ToString(formatString, CultureInfo), time);
	}

	private void UpdateListBoxSelectedItem()
	{
		if (_timeListBox == null)
		{
			return;
		}
		TimeItem timeItem = null;
		if (Value.HasValue)
		{
			timeItem = CreateTimeItem(Value.Value.TimeOfDay);
			if (!_timeListBox.Items.Contains(timeItem))
			{
				timeItem = null;
			}
		}
		_timeListBox.SelectedItem = timeItem;
	}

	private void InvalidateListBoxItems()
	{
		_isListBoxInvalid = true;
		if (IsOpen)
		{
			UpdateListBoxItems();
		}
	}

	private void UpdateListBoxItems()
	{
		if (_timeListBox != null && _isListBoxInvalid)
		{
			_timeListBox.ItemsSource = GenerateTimeListItemsSource();
			UpdateListBoxSelectedItem();
			_isListBoxInvalid = false;
		}
	}

	private TimeItem GetNearestTimeItem(TimeSpan time)
	{
		if (_timeListBox != null)
		{
			int count = _timeListBox.Items.Count;
			for (int i = 0; i < count; i++)
			{
				if (_timeListBox.Items[i] is TimeItem timeItem && timeItem.Time >= time)
				{
					return timeItem;
				}
			}
			if (count > 0)
			{
				return _timeListBox.Items[count - 1] as TimeItem;
			}
		}
		return null;
	}
}
