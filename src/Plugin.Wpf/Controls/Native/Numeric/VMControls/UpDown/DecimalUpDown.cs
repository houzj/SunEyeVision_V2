using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

public class DecimalUpDown : CommonNumericUpDown<decimal>
{
	static DecimalUpDown()
	{
		CommonNumericUpDown<decimal>.UpdateMetadata(typeof(DecimalUpDown), 1m, decimal.MinValue, decimal.MaxValue);
	}

	public DecimalUpDown()
		: base((FromText)decimal.TryParse, (FromDecimal)((decimal d) => d), (Func<decimal, decimal, bool>)((decimal v1, decimal v2) => v1 < v2), (Func<decimal, decimal, bool>)((decimal v1, decimal v2) => v1 > v2))
	{
	}

	protected override decimal IncrementValue(decimal value, decimal increment)
	{
		return value + increment;
	}

	protected override decimal DecrementValue(decimal value, decimal increment)
	{
		return value - increment;
	}
}
