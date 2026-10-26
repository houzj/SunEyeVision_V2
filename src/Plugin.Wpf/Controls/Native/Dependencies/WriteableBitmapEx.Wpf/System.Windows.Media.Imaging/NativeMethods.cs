using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using System.Runtime;
using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;

internal static class NativeMethods
{
	[TargetedPatchingOptOut("Internal method only, inlined across NGen boundaries for performance reasons")]
	internal unsafe static void CopyUnmanagedMemory(byte* srcPtr, int srcOffset, byte* dstPtr, int dstOffset, int count)
	{
		srcPtr += srcOffset;
		dstPtr += dstOffset;
		memcpy(dstPtr, srcPtr, count);
	}

	[TargetedPatchingOptOut("Internal method only, inlined across NGen boundaries for performance reasons")]
	internal static void SetUnmanagedMemory(IntPtr dst, int filler, int count)
	{
		memset(dst, filler, count);
	}

	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	private unsafe static extern byte* memcpy(byte* dst, byte* src, int count);

	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl)]
	private static extern void memset(IntPtr dst, int filler, int count);
}
