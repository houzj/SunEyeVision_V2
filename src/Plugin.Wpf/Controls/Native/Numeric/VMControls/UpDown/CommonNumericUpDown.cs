using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

public abstract class CommonNumericUpDown<T> : NumericUpDown<T?> where T : struct, IFormattable, IComparable<T>
{
	protected delegate bool FromText(string s, NumberStyles style, IFormatProvider provider, out T result);

	protected delegate T FromDecimal(decimal d);

	private FromText _fromText;

	private FromDecimal _fromDecimal;

	private Func<T, T, bool> _fromLowerThan;

	private Func<T, T, bool> _fromGreaterThan;

	public static readonly DependencyProperty ParsingNumberStyleProperty = DependencyProperty.Register("ParsingNumberStyle", typeof(NumberStyles), typeof(CommonNumericUpDown<T>), new UIPropertyMetadata(NumberStyles.Any));

	public NumberStyles ParsingNumberStyle
	{
		get
		{
			return (NumberStyles)GetValue(ParsingNumberStyleProperty);
		}
		set
		{
			SetValue(ParsingNumberStyleProperty, value);
		}
	}

	protected CommonNumericUpDown(FromText fromText, FromDecimal fromDecimal, Func<T, T, bool> fromLowerThan, Func<T, T, bool> fromGreaterThan)
	{
		if (fromText == null)
		{
			throw new ArgumentNullException("tryParseMethod");
		}
		if (fromDecimal == null)
		{
			throw new ArgumentNullException("fromDecimal");
		}
		if (fromLowerThan == null)
		{
			throw new ArgumentNullException("fromLowerThan");
		}
		if (fromGreaterThan == null)
		{
			throw new ArgumentNullException("fromGreaterThan");
		}
		_fromText = fromText;
		_fromDecimal = fromDecimal;
		_fromLowerThan = fromLowerThan;
		_fromGreaterThan = fromGreaterThan;
	}

	protected static void UpdateMetadata(Type type, T? increment, T? minValue, T? maxValue)
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(type));
		UpdateMetadataCommon(type, increment, minValue, maxValue);
	}

	private static void UpdateMetadataCommon(Type type, T? increment, T? minValue, T? maxValue)
	{
		NumericUpDown<T?>.IncrementProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(increment));
		UpDownBase<T?>.MaximumProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(maxValue));
		UpDownBase<T?>.MinimumProperty.OverrideMetadata(type, new FrameworkPropertyMetadata(minValue));
	}

	protected void TestInputSpecialValue(AllowedSpecialValues allowedValues, AllowedSpecialValues valueToCompare)
	{
		if ((allowedValues & valueToCompare) != valueToCompare)
		{
			switch (valueToCompare)
			{
			case AllowedSpecialValues.NaN:
				throw new InvalidDataException("Value to parse shouldn't be NaN.");
			case AllowedSpecialValues.PositiveInfinity:
				throw new InvalidDataException("Value to parse shouldn't be Positive Infinity.");
			case AllowedSpecialValues.NegativeInfinity:
				throw new InvalidDataException("Value to parse shouldn't be Negative Infinity.");
			case AllowedSpecialValues.NaN | AllowedSpecialValues.PositiveInfinity:
				break;
			}
		}
	}

	private bool IsLowerThan(T? value1, T? value2)
	{
		if (!value1.HasValue || !value2.HasValue)
		{
			return false;
		}
		return _fromLowerThan(value1.Value, value2.Value);
	}

	private bool IsGreaterThan(T? value1, T? value2)
	{
		if (!value1.HasValue || !value2.HasValue)
		{
			return false;
		}
		return _fromGreaterThan(value1.Value, value2.Value);
	}

	private bool HandleNullSpin()
	{
		if (!Value.HasValue)
		{
			T value = (DefaultValue.HasValue ? DefaultValue.Value : default(T));
			Value = CoerceValueMinMax(value);
			return true;
		}
		if (!Increment.HasValue)
		{
			return true;
		}
		return false;
	}

	internal bool IsValid(T? value)
	{
		if (!IsLowerThan(value, Minimum))
		{
			return !IsGreaterThan(value, Maximum);
		}
		return false;
	}

	private T? CoerceValueMinMax(T value)
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

	protected override void OnIncrement()
	{
		if (!HandleNullSpin())
		{
			if (UpdateValueOnEnterKey)
			{
				T value = IncrementValue(ConvertTextToValue(TextBox.Text).Value, Increment.Value);
				T? val = CoerceValueMinMax(value);
				TextBox.Text = val.Value.ToString(FormatString, CultureInfo);
			}
			else
			{
				T value2 = IncrementValue(Value.Value, Increment.Value);
				Value = CoerceValueMinMax(value2);
			}
		}
	}

	protected override void OnDecrement()
	{
		if (!HandleNullSpin())
		{
			if (UpdateValueOnEnterKey)
			{
				T value = DecrementValue(ConvertTextToValue(TextBox.Text).Value, Increment.Value);
				T? val = CoerceValueMinMax(value);
				TextBox.Text = val.Value.ToString(FormatString, CultureInfo);
			}
			else
			{
				T value2 = DecrementValue(Value.Value, Increment.Value);
				Value = CoerceValueMinMax(value2);
			}
		}
	}

	protected override void OnMinimumChanged(T? oldValue, T? newValue)
	{
		base.OnMinimumChanged(oldValue, newValue);
		if (Value.HasValue && ClipValueToMinMax)
		{
			Value = CoerceValueMinMax(Value.Value);
		}
	}

	protected override void OnMaximumChanged(T? oldValue, T? newValue)
	{
		base.OnMaximumChanged(oldValue, newValue);
		if (Value.HasValue && ClipValueToMinMax)
		{
			Value = CoerceValueMinMax(Value.Value);
		}
	}

	protected override T? ConvertTextToValue(string text)
	{
		T? result = null;
		if (string.IsNullOrEmpty(text))
		{
			return result;
		}
		string text2 = ConvertValueToText();
		if (object.Equals(text2, text))
		{
			return Value;
		}
		result = ConvertTextToValueCore(text2, text);
		if (ClipValueToMinMax)
		{
			return GetClippedMinMaxValue(result);
		}
		ValidateDefaultMinMax(result);
		return result;
	}

	protected override string ConvertValueToText()
	{
		if (!Value.HasValue)
		{
			return string.Empty;
		}
		if (FormatString.Contains("{0"))
		{
			return string.Format(CultureInfo, FormatString, new object[1] { Value.Value });
		}
		return Value.Value.ToString(FormatString, CultureInfo);
	}

	protected override void SetValidSpinDirection()
	{
		ValidSpinDirections validSpinDirections = ValidSpinDirections.None;
		if (Increment.HasValue && !IsReadOnly)
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

	private bool IsPercent(string stringToTest)
	{
		int num = stringToTest.IndexOf("P");
		if (num >= 0)
		{
			return !stringToTest.Substring(0, num).Contains("'") || !stringToTest.Substring(num, FormatString.Length - num).Contains("'");
		}
		return false;
	}

	private T? ConvertTextToValueCore(string currentValueText, string text)
	{
		T? result;
		if (IsPercent(FormatString))
		{
			result = _fromDecimal(NumericUpDown<T?>.ParsePercent(text, CultureInfo));
		}
		else
		{
			T result2 = new T();
			if (!_fromText(text, ParsingNumberStyle, CultureInfo, out result2))
			{
				bool flag = true;
				if (!_fromText(currentValueText, ParsingNumberStyle, CultureInfo, out var _))
				{
					IEnumerable<char> first = currentValueText.Where((char c) => !char.IsDigit(c));
					IEnumerable<char> enumerable = text.Where((char c) => !char.IsDigit(c));
					if (first.Except(enumerable).ToList().Count == 0)
					{
						foreach (char item in enumerable)
						{
							text = text.Replace(item.ToString(), string.Empty);
						}
						if (_fromText(text, ParsingNumberStyle, CultureInfo, out result2))
						{
							flag = false;
						}
					}
				}
				if (flag)
				{
					throw new InvalidDataException("Input string was not in a correct format.");
				}
			}
			result = result2;
		}
		return result;
	}

	private T? GetClippedMinMaxValue(T? result)
	{
		if (IsGreaterThan(result, Maximum))
		{
			return Maximum;
		}
		if (IsLowerThan(result, Minimum))
		{
			return Minimum;
		}
		return result;
	}

	private void ValidateDefaultMinMax(T? value)
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

	protected abstract T IncrementValue(T value, T increment);

	protected abstract T DecrementValue(T value, T increment);
}
