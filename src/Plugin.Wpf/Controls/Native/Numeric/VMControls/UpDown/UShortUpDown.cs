using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;

internal class UShortUpDown : CommonNumericUpDown<ushort>
{
	static UShortUpDown()
	{
		CommonNumericUpDown<ushort>.UpdateMetadata(typeof(UShortUpDown), (ushort)1, 0, ushort.MaxValue);
	}

	public UShortUpDown()
		: base((FromText)ushort.TryParse, (FromDecimal)decimal.ToUInt16, (Func<ushort, ushort, bool>)((ushort v1, ushort v2) => v1 < v2), (Func<ushort, ushort, bool>)((ushort v1, ushort v2) => v1 > v2))
	{
	}

	protected override ushort IncrementValue(ushort value, ushort increment)
	{
		return (ushort)(value + increment);
	}

	protected override ushort DecrementValue(ushort value, ushort increment)
	{
		return (ushort)(value - increment);
	}
}
