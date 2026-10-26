using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

internal class SByteUpDown : CommonNumericUpDown<sbyte>
{
	static SByteUpDown()
	{
		CommonNumericUpDown<sbyte>.UpdateMetadata(typeof(SByteUpDown), (sbyte)1, sbyte.MinValue, sbyte.MaxValue);
	}

	public SByteUpDown()
		: base((FromText)sbyte.TryParse, (FromDecimal)decimal.ToSByte, (Func<sbyte, sbyte, bool>)((sbyte v1, sbyte v2) => v1 < v2), (Func<sbyte, sbyte, bool>)((sbyte v1, sbyte v2) => v1 > v2))
	{
	}

	protected override sbyte IncrementValue(sbyte value, sbyte increment)
	{
		return (sbyte)(value + increment);
	}

	protected override sbyte DecrementValue(sbyte value, sbyte increment)
	{
		return (sbyte)(value - increment);
	}
}
