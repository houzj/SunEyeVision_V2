using SunEyeVision.Plugin.Wpf.Controls.Foundation;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Controls.Foundation;

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
