using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

public class IntegerUpDown : CommonNumericUpDown<int>
{
	static IntegerUpDown()
	{
		CommonNumericUpDown<int>.UpdateMetadata(typeof(IntegerUpDown), 1, int.MinValue, int.MaxValue);
	}

	public IntegerUpDown()
		: base((FromText)int.TryParse, (FromDecimal)decimal.ToInt32, (Func<int, int, bool>)((int v1, int v2) => v1 < v2), (Func<int, int, bool>)((int v1, int v2) => v1 > v2))
	{
	}

	protected override int IncrementValue(int value, int increment)
	{
		return value + increment;
	}

	protected override int DecrementValue(int value, int increment)
	{
		return value - increment;
	}
}
