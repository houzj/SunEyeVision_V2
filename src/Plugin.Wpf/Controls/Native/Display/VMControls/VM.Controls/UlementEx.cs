using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public static class UlementEx
{
	public static double RoundLayoutValue(double value, double dpiScale)
	{
		double num;
		if (!DoubleUtil.AreClose(dpiScale, 1.0))
		{
			num = Math.Round(value * dpiScale) / dpiScale;
			if (DoubleUtil.IsNaN(num) || double.IsInfinity(num) || DoubleUtil.AreClose(num, double.MaxValue))
			{
				num = value;
			}
		}
		else
		{
			num = Math.Round(value);
		}
		return num;
	}
}
