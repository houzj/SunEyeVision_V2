using System;
using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.HikExternalCall.Common;

public static class ExternalCallHelper
{
	public static byte[] IntPtr2Bytes(IntPtr ptr, int size)
	{
		if (IntPtr.Zero == ptr || size == 0)
		{
			return null;
		}
		byte[] array = new byte[size];
		Marshal.Copy(ptr, array, 0, size);
		return array;
	}
}
