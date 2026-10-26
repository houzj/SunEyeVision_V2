using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

public abstract class DateTimeUpDownBase<T> : UpDownBase<T>
{
	internal List<DateTimeInfo> _dateTimeInfoList = new List<DateTimeInfo>();

	internal DateTimeInfo _selectedDateTimeInfo;

	internal bool _fireSelectionChangedEvent = true;

	internal bool _processTextChanged = true;

	public static readonly DependencyProperty CurrentDateTimePartProperty = DependencyProperty.Register("CurrentDateTimePart", typeof(DateTimePart), typeof(DateTimeUpDownBase<T>), new UIPropertyMetadata(DateTimePart.Other, OnCurrentDateTimePartChanged));

	public static readonly DependencyProperty StepProperty = DependencyProperty.Register("Step", typeof(int), typeof(DateTimeUpDownBase<T>), new UIPropertyMetadata(1, OnStepChanged));

	public DateTimePart CurrentDateTimePart
	{
		get
		{
			return (DateTimePart)GetValue(CurrentDateTimePartProperty);
		}
		set
		{
			SetValue(CurrentDateTimePartProperty, value);
		}
	}

	public int Step
	{
		get
		{
			return (int)GetValue(StepProperty);
		}
		set
		{
			SetValue(StepProperty, value);
		}
	}

	private static void OnCurrentDateTimePartChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is DateTimeUpDownBase<T> dateTimeUpDownBase)
		{
			dateTimeUpDownBase.OnCurrentDateTimePartChanged((DateTimePart)e.OldValue, (DateTimePart)e.NewValue);
		}
	}

	protected virtual void OnCurrentDateTimePartChanged(DateTimePart oldValue, DateTimePart newValue)
	{
		Select(GetDateTimeInfo(newValue));
	}

	private static void OnStepChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is DateTimeUpDownBase<T> dateTimeUpDownBase)
		{
			dateTimeUpDownBase.OnStepChanged((int)e.OldValue, (int)e.NewValue);
		}
	}

	protected virtual void OnStepChanged(int oldValue, int newValue)
	{
	}

	internal DateTimeUpDownBase()
	{
		InitializeDateTimeInfoList(Value);
		Loaded += DateTimeUpDownBase_Loaded;
	}

	public override void OnApplyTemplate()
	{
		if (TextBox != null)
		{
			TextBox.SelectionChanged -= TextBox_SelectionChanged;
		}
		base.OnApplyTemplate();
		if (TextBox != null)
		{
			TextBox.SelectionChanged += TextBox_SelectionChanged;
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		int num = ((_selectedDateTimeInfo != null) ? _selectedDateTimeInfo.StartPosition : 0);
		int num2 = ((_selectedDateTimeInfo != null) ? _selectedDateTimeInfo.Length : 0);
		switch (e.Key)
		{
		case Key.Return:
			if (!IsReadOnly)
			{
				_fireSelectionChangedEvent = false;
				BindingOperations.GetBindingExpression(TextBox, TextBox.TextProperty).UpdateSource();
				_fireSelectionChangedEvent = true;
			}
			return;
		case Key.Add:
			if (AllowSpin && !IsReadOnly)
			{
				DoIncrement();
				e.Handled = true;
			}
			_fireSelectionChangedEvent = false;
			break;
		case Key.Subtract:
			if (AllowSpin && !IsReadOnly)
			{
				DoDecrement();
				e.Handled = true;
			}
			_fireSelectionChangedEvent = false;
			break;
		case Key.Right:
			if (IsCurrentValueValid())
			{
				PerformKeyboardSelection(num + num2);
				e.Handled = true;
			}
			_fireSelectionChangedEvent = false;
			break;
		case Key.Left:
			if (IsCurrentValueValid())
			{
				PerformKeyboardSelection((num > 0) ? (num - 1) : 0);
				e.Handled = true;
			}
			_fireSelectionChangedEvent = false;
			break;
		default:
			_fireSelectionChangedEvent = false;
			break;
		}
		base.OnPreviewKeyDown(e);
	}

	private void TextBox_SelectionChanged(object sender, RoutedEventArgs e)
	{
		if (_fireSelectionChangedEvent)
		{
			PerformMouseSelection();
		}
		else
		{
			_fireSelectionChangedEvent = true;
		}
	}

	private void DateTimeUpDownBase_Loaded(object sender, RoutedEventArgs e)
	{
		InitSelection();
	}

	protected virtual void InitializeDateTimeInfoList(T value)
	{
	}

	protected virtual bool IsCurrentValueValid()
	{
		return true;
	}

	protected virtual void PerformMouseSelection()
	{
		DateTimeInfo dateTimeInfo = GetDateTimeInfo(TextBox.SelectionStart);
		if (dateTimeInfo != null && dateTimeInfo.Type == DateTimePart.Other)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() =>
			{
				Select(GetDateTimeInfo(dateTimeInfo.StartPosition + dateTimeInfo.Length));
			}));
		}
		else
		{
			Select(dateTimeInfo);
		}
	}

	protected virtual bool IsLowerThan(T value1, T value2)
	{
		return false;
	}

	protected virtual bool IsGreaterThan(T value1, T value2)
	{
		return false;
	}

	internal DateTimeInfo GetDateTimeInfo(int selectionStart)
	{
		return _dateTimeInfoList.FirstOrDefault((DateTimeInfo info) => info.StartPosition <= selectionStart && selectionStart < info.StartPosition + info.Length);
	}

	internal DateTimeInfo GetDateTimeInfo(DateTimePart part)
	{
		return _dateTimeInfoList.FirstOrDefault((DateTimeInfo info) => info.Type == part);
	}

	internal void Select(DateTimeInfo info)
	{
		if (info != null && !info.Equals(_selectedDateTimeInfo) && TextBox != null && !string.IsNullOrEmpty(TextBox.Text))
		{
			_fireSelectionChangedEvent = false;
			TextBox.Select(info.StartPosition, info.Length);
			_fireSelectionChangedEvent = true;
			_selectedDateTimeInfo = info;
			SetCurrentValue(CurrentDateTimePartProperty, info.Type);
		}
	}

	internal T CoerceValueMinMax(T value)
	{
		if (IsLowerThan(value, Minimum))
		{
			return Minimum;
		}
		if (IsGreaterThan(value, Maximum))
		{
			return Maximum;
		}
		return value;
	}

	internal void ValidateDefaultMinMax(T value)
	{
		if (!object.Equals(value, DefaultValue))
		{
			if (IsLowerThan(value, Minimum))
			{
				throw new ArgumentOutOfRangeException("Minimum", $"Value must be greater than MinValue of {Minimum}");
			}
			if (IsGreaterThan(value, Maximum))
			{
				throw new ArgumentOutOfRangeException("Maximum", $"Value must be less than MaxValue of {Maximum}");
			}
		}
	}

	internal T GetClippedMinMaxValue(T value)
	{
		if (IsGreaterThan(value, Maximum))
		{
			return Maximum;
		}
		if (IsLowerThan(value, Minimum))
		{
			return Minimum;
		}
		return value;
	}

	protected internal virtual void PerformKeyboardSelection(int nextSelectionStart)
	{
		TextBox.Focus();
		if (!UpdateValueOnEnterKey)
		{
			CommitInput();
		}
		int num = ((_selectedDateTimeInfo != null) ? _selectedDateTimeInfo.StartPosition : 0);
		if (nextSelectionStart - num > 0)
		{
			Select(GetNextDateTimeInfo(nextSelectionStart));
		}
		else
		{
			Select(GetPreviousDateTimeInfo(nextSelectionStart - 1));
		}
	}

	private DateTimeInfo GetNextDateTimeInfo(int nextSelectionStart)
	{
		DateTimeInfo dateTimeInfo = GetDateTimeInfo(nextSelectionStart);
		if (dateTimeInfo == null)
		{
			dateTimeInfo = _dateTimeInfoList.First();
		}
		DateTimeInfo objB = dateTimeInfo;
		while (dateTimeInfo.Type == DateTimePart.Other)
		{
			dateTimeInfo = GetDateTimeInfo(dateTimeInfo.StartPosition + dateTimeInfo.Length);
			if (dateTimeInfo == null)
			{
				dateTimeInfo = _dateTimeInfoList.First();
			}
			if (object.Equals(dateTimeInfo, objB))
			{
				throw new InvalidOperationException("Couldn't find a valid DateTimeInfo.");
			}
		}
		return dateTimeInfo;
	}

	private DateTimeInfo GetPreviousDateTimeInfo(int previousSelectionStart)
	{
		DateTimeInfo dateTimeInfo = GetDateTimeInfo(previousSelectionStart);
		if (dateTimeInfo == null && _dateTimeInfoList.Count > 0)
		{
			dateTimeInfo = _dateTimeInfoList.Last();
		}
		DateTimeInfo objB = dateTimeInfo;
		while (dateTimeInfo != null && dateTimeInfo.Type == DateTimePart.Other)
		{
			dateTimeInfo = GetDateTimeInfo(dateTimeInfo.StartPosition - 1);
			if (dateTimeInfo == null)
			{
				dateTimeInfo = _dateTimeInfoList.Last();
			}
			if (object.Equals(dateTimeInfo, objB))
			{
				throw new InvalidOperationException("Couldn't find a valid DateTimeInfo.");
			}
		}
		return dateTimeInfo;
	}

	private void InitSelection()
	{
		if (_selectedDateTimeInfo == null)
		{
			Select((CurrentDateTimePart != DateTimePart.Other) ? GetDateTimeInfo(CurrentDateTimePart) : GetDateTimeInfo(0));
		}
	}
}
