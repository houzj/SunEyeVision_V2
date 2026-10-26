using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal static class DoubleHelper
{
	[StructLayout(LayoutKind.Explicit)]
	private struct NanUnion
	{
		[FieldOffset(0)]
		internal double DoubleValue;

		[FieldOffset(0)]
		internal ulong UintValue;
	}

	public static bool AreVirtuallyEqual(double d1, double d2)
	{
		if (double.IsPositiveInfinity(d1))
		{
			return double.IsPositiveInfinity(d2);
		}
		if (double.IsNegativeInfinity(d1))
		{
			return double.IsNegativeInfinity(d2);
		}
		if (IsNaN(d1))
		{
			return IsNaN(d2);
		}
		double num = d1 - d2;
		double num2 = (Math.Abs(d1) + Math.Abs(d2) + 10.0) * 1E-15;
		if (0.0 - num2 < num)
		{
			return num2 > num;
		}
		return false;
	}

	public static bool AreVirtuallyEqual(Size s1, Size s2)
	{
		if (AreVirtuallyEqual(s1.Width, s2.Width))
		{
			return AreVirtuallyEqual(s1.Height, s2.Height);
		}
		return false;
	}

	public static bool AreVirtuallyEqual(Point p1, Point p2)
	{
		if (AreVirtuallyEqual(p1.X, p2.X))
		{
			return AreVirtuallyEqual(p1.Y, p2.Y);
		}
		return false;
	}

	public static bool AreVirtuallyEqual(Rect r1, Rect r2)
	{
		if (AreVirtuallyEqual(r1.TopLeft, r2.TopLeft))
		{
			return AreVirtuallyEqual(r1.BottomRight, r2.BottomRight);
		}
		return false;
	}

	public static bool AreVirtuallyEqual(Vector v1, Vector v2)
	{
		if (AreVirtuallyEqual(v1.X, v2.X))
		{
			return AreVirtuallyEqual(v1.Y, v2.Y);
		}
		return false;
	}

	public static bool AreVirtuallyEqual(Segment s1, Segment s2)
	{
		return s1 == s2;
	}

	public static bool IsNaN(double value)
	{
		NanUnion nanUnion = new NanUnion
		{
			DoubleValue = value
		};
		ulong num = nanUnion.UintValue & 0xFFF0000000000000uL;
		ulong num2 = nanUnion.UintValue & 0xFFFFFFFFFFFFFL;
		if (num == 9218868437227405312L || num == 18442240474082181120uL)
		{
			return num2 != 0;
		}
		return false;
	}
}
