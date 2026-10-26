using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

public class ShortUpDown : CommonNumericUpDown<short>
{
	static ShortUpDown()
	{
		CommonNumericUpDown<short>.UpdateMetadata(typeof(ShortUpDown), (short)1, short.MinValue, short.MaxValue);
	}

	public ShortUpDown()
		: base((FromText)short.TryParse, (FromDecimal)decimal.ToInt16, (Func<short, short, bool>)((short v1, short v2) => v1 < v2), (Func<short, short, bool>)((short v1, short v2) => v1 > v2))
	{
	}

	protected override short IncrementValue(short value, short increment)
	{
		return (short)(value + increment);
	}

	protected override short DecrementValue(short value, short increment)
	{
		return (short)(value - increment);
	}
}
