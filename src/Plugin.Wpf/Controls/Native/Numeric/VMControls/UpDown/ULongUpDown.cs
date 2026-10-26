using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

internal class ULongUpDown : CommonNumericUpDown<ulong>
{
	static ULongUpDown()
	{
		CommonNumericUpDown<ulong>.UpdateMetadata(typeof(ULongUpDown), 1uL, 0uL, ulong.MaxValue);
	}

	public ULongUpDown()
		: base((FromText)ulong.TryParse, (FromDecimal)decimal.ToUInt64, (Func<ulong, ulong, bool>)((ulong v1, ulong v2) => v1 < v2), (Func<ulong, ulong, bool>)((ulong v1, ulong v2) => v1 > v2))
	{
	}

	protected override ulong IncrementValue(ulong value, ulong increment)
	{
		return value + increment;
	}

	protected override ulong DecrementValue(ulong value, ulong increment)
	{
		return value - increment;
	}
}
