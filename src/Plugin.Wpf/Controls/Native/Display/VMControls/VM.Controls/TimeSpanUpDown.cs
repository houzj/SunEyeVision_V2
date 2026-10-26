using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class TimeSpanUpDown : DateTimeUpDownBase<TimeSpan?>
{
	public static readonly DependencyProperty FractionalSecondsDigitsCountProperty;

	public static readonly DependencyProperty ShowSecondsProperty;

	public int FractionalSecondsDigitsCount
	{
		get
		{
			return (int)GetValue(FractionalSecondsDigitsCountProperty);
		}
		set
		{
			SetValue(FractionalSecondsDigitsCountProperty, value);
		}
	}

	public bool ShowSeconds
	{
		get
		{
			return (bool)GetValue(ShowSecondsProperty);
		}
		set
		{
			SetValue(ShowSecondsProperty, value);
		}
	}

	static TimeSpanUpDown()
	{
		FractionalSecondsDigitsCountProperty = DependencyProperty.Register("FractionalSecondsDigitsCount", typeof(int), typeof(TimeSpanUpDown), new UIPropertyMetadata(0, OnFractionalSecondsDigitsCountChanged, OnCoerceFractionalSecondsDigitsCount));
		ShowSecondsProperty = DependencyProperty.Register("ShowSeconds", typeof(bool), typeof(TimeSpanUpDown), new UIPropertyMetadata(true, OnShowSecondsChanged));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(TimeSpanUpDown), new FrameworkPropertyMetadata(typeof(TimeSpanUpDown)));
		UpDownBase<TimeSpan?>.MaximumProperty.OverrideMetadata(typeof(TimeSpanUpDown), new FrameworkPropertyMetadata(TimeSpan.MaxValue));
		UpDownBase<TimeSpan?>.MinimumProperty.OverrideMetadata(typeof(TimeSpanUpDown), new FrameworkPropertyMetadata(TimeSpan.MinValue));
		UpDownBase<TimeSpan?>.DefaultValueProperty.OverrideMetadata(typeof(TimeSpanUpDown), new FrameworkPropertyMetadata(TimeSpan.Zero));
	}

	public TimeSpanUpDown()
	{
		DataObject.AddPastingHandler(this, OnPasting);
	}

	private static object OnCoerceFractionalSecondsDigitsCount(DependencyObject o, object value)
	{
		if (o is TimeSpanUpDown)
		{
			int num = (int)value;
			if (num < 0 || num > 3)
			{
				throw new ArgumentException("Fractional seconds digits count must be between 0 and 3.");
			}
		}
		return value;
	}

	private static void OnFractionalSecondsDigitsCountChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is TimeSpanUpDown timeSpanUpDown)
		{
			timeSpanUpDown.OnFractionalSecondsDigitsCountChanged((int)e.OldValue, (int)e.NewValue);
		}
	}

	protected virtual void OnFractionalSecondsDigitsCountChanged(int oldValue, int newValue)
	{
		TimeSpan? value;
		if (UpdateValueOnEnterKey)
		{
			value = ((TextBox != null) ? ConvertTextToValue(TextBox.Text) : ((TimeSpan?)null));
		}
		else
		{
			value = Value;
		}
		InitializeDateTimeInfoList(value);
		SyncTextAndValueProperties(updateValueFromText: false, Text);
	}

	private static void OnShowSecondsChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is TimeSpanUpDown timeSpanUpDown)
		{
			timeSpanUpDown.OnShowSecondsChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnShowSecondsChanged(bool oldValue, bool newValue)
	{
		TimeSpan? value;
		if (UpdateValueOnEnterKey)
		{
			value = ((TextBox != null) ? ConvertTextToValue(TextBox.Text) : ((TimeSpan?)null));
		}
		else
		{
			value = Value;
		}
		InitializeDateTimeInfoList(value);
		SyncTextAndValueProperties(updateValueFromText: false, Text);
	}

	protected override void OnCultureInfoChanged(CultureInfo oldValue, CultureInfo newValue)
	{
		TimeSpan? value;
		if (UpdateValueOnEnterKey)
		{
			value = ((TextBox != null) ? ConvertTextToValue(TextBox.Text) : ((TimeSpan?)null));
		}
		else
		{
			value = Value;
		}
		InitializeDateTimeInfoList(value);
	}

	protected override void SetValidSpinDirection()
	{
		ValidSpinDirections validSpinDirections = ValidSpinDirections.None;
		if (!IsReadOnly)
		{
			if (IsLowerThan(Value, Maximum) || !Value.HasValue || !Maximum.HasValue)
			{
				validSpinDirections |= ValidSpinDirections.Increase;
			}
			if (IsGreaterThan(Value, Minimum) || !Value.HasValue || !Minimum.HasValue)
			{
				validSpinDirections |= ValidSpinDirections.Decrease;
			}
		}
		if (Spinner != null)
		{
			Spinner.ValidSpinDirection = validSpinDirections;
		}
	}

	protected override void OnIncrement()
	{
		Increment(Step);
	}

	protected override void OnDecrement()
	{
		Increment(-Step);
	}

	protected override string ConvertValueToText()
	{
		if (!Value.HasValue)
		{
			return string.Empty;
		}
		return ParseValueIntoTimeSpanInfo(Value);
	}

	protected override TimeSpan? ConvertTextToValue(string text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return null;
		}
		TimeSpan value = TimeSpan.Parse(text);
		if (ClipValueToMinMax)
		{
			return GetClippedMinMaxValue(value);
		}
		ValidateDefaultMinMax(value);
		return value;
	}

	protected override void OnPreviewTextInput(TextCompositionEventArgs e)
	{
		e.Handled = !IsNumber(e.Text);
		base.OnPreviewTextInput(e);
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
		base.OnPreviewKeyDown(e);
	}

	protected override void OnTextChanged(string previousValue, string currentValue)
	{
		if (!_processTextChanged)
		{
			return;
		}
		if (string.IsNullOrEmpty(currentValue))
		{
			if (!UpdateValueOnEnterKey)
			{
				Value = null;
			}
			return;
		}
		if (Value.HasValue)
		{
			_ = Value.Value;
		}
		if (!TimeSpan.TryParse(currentValue, out var result))
		{
			List<char> source = currentValue.Where((char x) => x == ':' || x == '.').ToList();
			string[] array = currentValue.Split(':', '.');
			if (array.Count() >= 2)
			{
				bool flag = source.First() == '.';
				bool flag2 = source.Count() > 1 && source.Last() == '.';
				result = new TimeSpan(flag ? int.Parse(array[0]) : 0, flag ? int.Parse(array[1]) : int.Parse(array[0]), flag ? int.Parse(array[2]) : int.Parse(array[1]), (flag && ShowSeconds) ? int.Parse(array[3]) : (ShowSeconds ? int.Parse(array[2]) : 0), flag2 ? int.Parse(array.Last()) : 0);
			}
		}
		currentValue = result.ToString();
		if ((_isTextChangedFromUI && !UpdateValueOnEnterKey) || !_isTextChangedFromUI)
		{
			SyncTextAndValueProperties(updateValueFromText: true, currentValue);
		}
	}

	protected override void OnValueChanged(TimeSpan? oldValue, TimeSpan? newValue)
	{
		if (newValue.HasValue)
		{
			TimeSpan? value;
			if (UpdateValueOnEnterKey)
			{
				value = ((TextBox != null) ? ConvertTextToValue(TextBox.Text) : ((TimeSpan?)null));
			}
			else
			{
				value = Value;
			}
			InitializeDateTimeInfoList(value);
		}
		base.OnValueChanged(oldValue, newValue);
	}

	protected override void PerformMouseSelection()
	{
		TimeSpan? value;
		if (UpdateValueOnEnterKey)
		{
			value = ((TextBox != null) ? ConvertTextToValue(TextBox.Text) : ((TimeSpan?)null));
		}
		else
		{
			value = Value;
		}
		InitializeDateTimeInfoList(value);
		base.PerformMouseSelection();
	}

	protected override void InitializeDateTimeInfoList(TimeSpan? value)
	{
		DateTimeInfo dateTimeInfo = _dateTimeInfoList.FirstOrDefault((DateTimeInfo x) => x.Type == DateTimePart.Day);
		bool flag = dateTimeInfo != null;
		DateTimeInfo dateTimeInfo2 = _dateTimeInfoList.FirstOrDefault((DateTimeInfo x) => x.Type == DateTimePart.Other);
		bool flag2 = dateTimeInfo2 != null && dateTimeInfo2.Content == "-";
		_dateTimeInfoList.Clear();
		if (value.HasValue && value.Value.TotalMilliseconds < 0.0)
		{
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Other,
				Length = 1,
				Content = "-",
				IsReadOnly = true
			});
			if (!flag2 && TextBox != null)
			{
				_fireSelectionChangedEvent = false;
				TextBox.SelectionStart++;
				_fireSelectionChangedEvent = true;
			}
		}
		if (value.HasValue && value.Value.Days != 0)
		{
			int length = Math.Abs(value.Value.Days).ToString().Length;
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Day,
				Length = length,
				Format = "dd"
			});
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Other,
				Length = 1,
				Content = ".",
				IsReadOnly = true
			});
			if (TextBox != null)
			{
				if (flag && length != dateTimeInfo.Length && _selectedDateTimeInfo.Type != DateTimePart.Day)
				{
					_fireSelectionChangedEvent = false;
					TextBox.SelectionStart = Math.Max(0, TextBox.SelectionStart + (length - dateTimeInfo.Length));
					_fireSelectionChangedEvent = true;
				}
				else if (!flag)
				{
					_fireSelectionChangedEvent = false;
					TextBox.SelectionStart += length + 1;
					_fireSelectionChangedEvent = true;
				}
			}
		}
		else if (flag)
		{
			_fireSelectionChangedEvent = false;
			TextBox.SelectionStart = Math.Max(flag2 ? 1 : 0, TextBox.SelectionStart - (dateTimeInfo.Length + 1));
			_fireSelectionChangedEvent = true;
		}
		_dateTimeInfoList.Add(new DateTimeInfo
		{
			Type = DateTimePart.Hour24,
			Length = 2,
			Format = "hh"
		});
		_dateTimeInfoList.Add(new DateTimeInfo
		{
			Type = DateTimePart.Other,
			Length = 1,
			Content = ":",
			IsReadOnly = true
		});
		_dateTimeInfoList.Add(new DateTimeInfo
		{
			Type = DateTimePart.Minute,
			Length = 2,
			Format = "mm"
		});
		if (ShowSeconds)
		{
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Other,
				Length = 1,
				Content = ":",
				IsReadOnly = true
			});
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Second,
				Length = 2,
				Format = "ss"
			});
		}
		if (FractionalSecondsDigitsCount > 0)
		{
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Other,
				Length = 1,
				Content = ".",
				IsReadOnly = true
			});
			string text = new string('f', FractionalSecondsDigitsCount);
			if (text.Length == 1)
			{
				text = "%" + text;
			}
			_dateTimeInfoList.Add(new DateTimeInfo
			{
				Type = DateTimePart.Millisecond,
				Length = FractionalSecondsDigitsCount,
				Format = text
			});
		}
		if (value.HasValue)
		{
			ParseValueIntoTimeSpanInfo(value);
		}
	}

	protected override bool IsLowerThan(TimeSpan? value1, TimeSpan? value2)
	{
		if (!value1.HasValue || !value2.HasValue)
		{
			return false;
		}
		return value1.Value < value2.Value;
	}

	protected override bool IsGreaterThan(TimeSpan? value1, TimeSpan? value2)
	{
		if (!value1.HasValue || !value2.HasValue)
		{
			return false;
		}
		return value1.Value > value2.Value;
	}

	private string ParseValueIntoTimeSpanInfo(TimeSpan? value)
	{
		string text = string.Empty;
		_dateTimeInfoList.ForEach((DateTimeInfo info) =>
		{
			if (info.Format == null)
			{
				info.StartPosition = text.Length;
				info.Length = info.Content.Length;
				text += info.Content;
			}
			else
			{
				TimeSpan timeSpan = TimeSpan.Parse(value.ToString());
				info.StartPosition = text.Length;
				info.Content = timeSpan.ToString(info.Format, CultureInfo.DateTimeFormat);
				if (info.Format == "dd")
				{
					info.Content = Convert.ToInt32(info.Content).ToString();
				}
				info.Length = info.Content.Length;
				text += info.Content;
			}
		});
		return text;
	}

	private TimeSpan? UpdateTimeSpan(TimeSpan? currentValue, int value)
	{
		DateTimeInfo dateTimeInfo = _selectedDateTimeInfo;
		if (dateTimeInfo == null)
		{
			if (CurrentDateTimePart != DateTimePart.Other)
			{
				dateTimeInfo = GetDateTimeInfo(CurrentDateTimePart);
			}
			else
			{
				dateTimeInfo = ((_dateTimeInfoList[0].Content != "-") ? _dateTimeInfoList[0] : _dateTimeInfoList[1]);
			}
		}
		TimeSpan? timeSpan = null;
		try
		{
			switch (dateTimeInfo.Type)
			{
			case DateTimePart.Day:
				timeSpan = currentValue.Value.Add(new TimeSpan(value, 0, 0, 0, 0));
				break;
			case DateTimePart.Hour24:
				timeSpan = currentValue.Value.Add(new TimeSpan(0, value, 0, 0, 0));
				break;
			case DateTimePart.Minute:
				timeSpan = currentValue.Value.Add(new TimeSpan(0, 0, value, 0, 0));
				break;
			case DateTimePart.Second:
				timeSpan = currentValue.Value.Add(new TimeSpan(0, 0, 0, value, 0));
				break;
			case DateTimePart.Millisecond:
				value = FractionalSecondsDigitsCount switch
				{
					1 => value * 100, 
					2 => value * 10, 
					_ => value, 
				};
				timeSpan = currentValue.Value.Add(new TimeSpan(0, 0, 0, 0, value));
				break;
			}
		}
		catch
		{
		}
		timeSpan = ((timeSpan.HasValue && timeSpan.HasValue) ? new TimeSpan?(timeSpan.Value) : timeSpan);
		return CoerceValueMinMax(timeSpan);
	}

	private void Increment(int step)
	{
		if (UpdateValueOnEnterKey)
		{
			TimeSpan? currentValue = ConvertTextToValue(TextBox.Text);
			TimeSpan? value = (currentValue.HasValue ? UpdateTimeSpan(currentValue, step) : new TimeSpan?(DefaultValue ?? TimeSpan.Zero));
			if (value.HasValue)
			{
				InitializeDateTimeInfoList(value);
				int selectionStart = TextBox.SelectionStart;
				int selectionLength = TextBox.SelectionLength;
				TextBox.Text = value.Value.ToString(GetTimeSpanFormat());
				TextBox.Select(selectionStart, selectionLength);
			}
		}
		else if (Value.HasValue)
		{
			TimeSpan? value2 = UpdateTimeSpan(Value, step);
			if (value2.HasValue)
			{
				InitializeDateTimeInfoList(value2);
				int selectionStart2 = TextBox.SelectionStart;
				int selectionLength2 = TextBox.SelectionLength;
				Value = value2;
				TextBox.Select(selectionStart2, selectionLength2);
			}
		}
		else
		{
			Value = DefaultValue ?? TimeSpan.Zero;
		}
	}

	private string GetTimeSpanFormat()
	{
		List<string> values = _dateTimeInfoList.Select((DateTimeInfo part) =>
		{
			if (part.Format == null)
			{
				return "\\" + part.Content;
			}
			if (part.Format.Contains('d'))
			{
				DateTimeInfo dateTimeInfo = _dateTimeInfoList.FirstOrDefault((DateTimeInfo info) => info.Type == DateTimePart.Day);
				part.Format = string.Join("", Enumerable.Repeat("d", dateTimeInfo.Length));
			}
			return part.Format;
		}).ToList();
		return string.Join("", values);
	}

	private bool IsNumber(string str)
	{
		for (int i = 0; i < str.Length; i++)
		{
			if (!char.IsNumber(str[i]))
			{
				return false;
			}
		}
		return true;
	}

	private void OnPasting(object sender, DataObjectPastingEventArgs e)
	{
		if (e.DataObject.GetDataPresent(typeof(string)) && !TimeSpan.TryParse(e.DataObject.GetData(typeof(string)) as string, out var _))
		{
			e.CancelCommand();
		}
	}
}
