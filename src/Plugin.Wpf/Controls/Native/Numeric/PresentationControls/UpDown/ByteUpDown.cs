using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;

public class ByteUpDown : CommonNumericUpDown<byte>
{
	static ByteUpDown()
	{
		CommonNumericUpDown<byte>.UpdateMetadata(typeof(ByteUpDown), (byte)1, 0, byte.MaxValue);
	}

	public ByteUpDown()
		: base((FromText)byte.TryParse, (FromDecimal)decimal.ToByte, (Func<byte, byte, bool>)((byte v1, byte v2) => v1 < v2), (Func<byte, byte, bool>)((byte v1, byte v2) => v1 > v2))
	{
	}

	protected override byte IncrementValue(byte value, byte increment)
	{
		return (byte)(value + increment);
	}

	protected override byte DecrementValue(byte value, byte increment)
	{
		return (byte)(value - increment);
	}
}
