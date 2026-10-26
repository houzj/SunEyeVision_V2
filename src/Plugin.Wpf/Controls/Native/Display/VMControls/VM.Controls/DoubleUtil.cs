using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Management;
using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public class DoubleUtil
{
	[StructLayout(LayoutKind.Explicit)]
	private struct NanUnion
	{
		[FieldOffset(0)]
		internal double DoubleValue;

		[FieldOffset(0)]
		internal readonly ulong UintValue;
	}

	public static double DpiScaleX
	{
		get
		{
			GetDPI(out var dpix, out var _);
			if (dpix != 96)
			{
				return (double)dpix / 96.0;
			}
			return 1.0;
		}
	}

	public static double DpiScaleY
	{
		get
		{
			GetDPI(out var _, out var dpiy);
			if (dpiy != 96)
			{
				return (double)dpiy / 96.0;
			}
			return 1.0;
		}
	}

	public static int DoubleToInt(double val)
	{
		if (!(0.0 < val))
		{
			return (int)(val - 0.5);
		}
		return (int)(val + 0.5);
	}

	public static void GetDPI(out int dpix, out int dpiy)
	{
		dpix = 0;
		dpiy = 0;
		using ManagementClass managementClass = new ManagementClass("Win32_DesktopMonitor");
		using ManagementObjectCollection managementObjectCollection = managementClass.GetInstances();
		foreach (ManagementObject item in managementObjectCollection)
		{
			dpix = int.Parse(item.Properties["PixelsPerXLogicalInch"].Value.ToString());
			dpiy = int.Parse(item.Properties["PixelsPerYLogicalInch"].Value.ToString());
		}
	}

	public static bool GreaterThan(double value1, double value2)
	{
		if (value1 > value2)
		{
			return !AreClose(value1, value2);
		}
		return false;
	}

	public static bool AreClose(double value1, double value2)
	{
		if (Math.Abs(value1 - value2) < 1E-06)
		{
			return true;
		}
		double num = (Math.Abs(value1) + Math.Abs(value2) + 10.0) * 2.220446049250313E-16;
		double num2 = value1 - value2;
		if (0.0 - num < num2)
		{
			return num > num2;
		}
		return false;
	}

	public static bool IsZero(double value)
	{
		return Math.Abs(value) < 2.220446049250313E-15;
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
