using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;

public class LongUpDown : CommonNumericUpDown<long>
{
	static LongUpDown()
	{
		CommonNumericUpDown<long>.UpdateMetadata(typeof(LongUpDown), 1L, long.MinValue, long.MaxValue);
	}

	public LongUpDown()
		: base((FromText)long.TryParse, (FromDecimal)decimal.ToInt64, (Func<long, long, bool>)((long v1, long v2) => v1 < v2), (Func<long, long, bool>)((long v1, long v2) => v1 > v2))
	{
	}

	protected override long IncrementValue(long value, long increment)
	{
		return value + increment;
	}

	protected override long DecrementValue(long value, long increment)
	{
		return value - increment;
	}
}
