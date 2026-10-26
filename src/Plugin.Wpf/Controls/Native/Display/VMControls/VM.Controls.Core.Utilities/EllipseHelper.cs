using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class EllipseHelper
{
	public static Point PointOfRadialIntersection(Rect ellipseRect, double angle)
	{
		double num = ellipseRect.Width / 2.0;
		double num2 = ellipseRect.Height / 2.0;
		double num3 = angle * Math.PI / 180.0;
		return RectHelper.Center(ellipseRect) + new Vector(num * Math.Cos(num3), num2 * Math.Sin(num3));
	}

	public static double RadialDistanceFromCenter(Rect ellipseRect, double angle)
	{
		double num = ellipseRect.Width / 2.0;
		double num2 = ellipseRect.Height / 2.0;
		double num3 = angle * Math.PI / 180.0;
		double num4 = Math.Sin(num3);
		double num5 = Math.Cos(num3);
		return Math.Sqrt(num * num * num2 * num2 / (num * num * num4 * num4 + num2 * num2 * num5 * num5));
	}
}
