using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

public class DoubleUpDown : CommonNumericUpDown<double>
{
	public static readonly DependencyProperty AllowInputSpecialValuesProperty;

	public AllowedSpecialValues AllowInputSpecialValues
	{
		get
		{
			return (AllowedSpecialValues)GetValue(AllowInputSpecialValuesProperty);
		}
		set
		{
			SetValue(AllowInputSpecialValuesProperty, value);
		}
	}

	static DoubleUpDown()
	{
		AllowInputSpecialValuesProperty = DependencyProperty.Register("AllowInputSpecialValues", typeof(AllowedSpecialValues), typeof(DoubleUpDown), new UIPropertyMetadata(AllowedSpecialValues.None));
		CommonNumericUpDown<double>.UpdateMetadata(typeof(DoubleUpDown), 1.0, double.NegativeInfinity, double.PositiveInfinity);
	}

	public DoubleUpDown()
		: base((FromText)double.TryParse, (FromDecimal)decimal.ToDouble, (Func<double, double, bool>)((double v1, double v2) => v1 < v2), (Func<double, double, bool>)((double v1, double v2) => v1 > v2))
	{
	}

	protected override double? OnCoerceIncrement(double? baseValue)
	{
		if (baseValue.HasValue && double.IsNaN(baseValue.Value))
		{
			throw new ArgumentException("NaN is invalid for Increment.");
		}
		return base.OnCoerceIncrement(baseValue);
	}

	protected override double? OnCoerceMaximum(double? baseValue)
	{
		if (baseValue.HasValue && double.IsNaN(baseValue.Value))
		{
			throw new ArgumentException("NaN is invalid for Maximum.");
		}
		return base.OnCoerceMaximum(baseValue);
	}

	protected override double? OnCoerceMinimum(double? baseValue)
	{
		if (baseValue.HasValue && double.IsNaN(baseValue.Value))
		{
			throw new ArgumentException("NaN is invalid for Minimum.");
		}
		return base.OnCoerceMinimum(baseValue);
	}

	protected override double IncrementValue(double value, double increment)
	{
		return value + increment;
	}

	protected override double DecrementValue(double value, double increment)
	{
		return value - increment;
	}

	protected override void SetValidSpinDirection()
	{
		if (Value.HasValue && double.IsInfinity(Value.Value) && Spinner != null)
		{
			Spinner.ValidSpinDirection = ValidSpinDirections.None;
		}
		else
		{
			base.SetValidSpinDirection();
		}
	}

	protected override double? ConvertTextToValue(string text)
	{
		double? result = base.ConvertTextToValue(text);
		if (result.HasValue)
		{
			if (double.IsNaN(result.Value))
			{
				TestInputSpecialValue(AllowInputSpecialValues, AllowedSpecialValues.NaN);
			}
			else if (double.IsPositiveInfinity(result.Value))
			{
				TestInputSpecialValue(AllowInputSpecialValues, AllowedSpecialValues.PositiveInfinity);
			}
			else if (double.IsNegativeInfinity(result.Value))
			{
				TestInputSpecialValue(AllowInputSpecialValues, AllowedSpecialValues.NegativeInfinity);
			}
		}
		return result;
	}
}
