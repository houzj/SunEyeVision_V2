using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using System;
using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;

internal static class MemoryNativeMethods
{
	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes, long flProtect, long dwMaximumSizeLow, long dwMaximumSizeHigh, string lpName);

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool FlushViewOfFile(IntPtr lpBaseAddress, IntPtr dwNumBytesToFlush);

	[DllImport("kernel32", SetLastError = true)]
	public static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, long dwDesiredAccess, long dwFileOffsetHigh, long dwFileOffsetLow, IntPtr dwNumBytesToMap);

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr OpenFileMapping(long dwDesiredAccess, bool bInheritHandle, string lpName);

	[DllImport("kernel32")]
	public static extern int GetLastError();

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool CloseHandle(IntPtr handle);

	[DllImport("gdi32.dll", SetLastError = true)]
	private static extern bool DeleteObject(IntPtr hObject);

	[DllImport("kernel32", SetLastError = true)]
	public static extern bool FlushViewOfFile(int lpBaseAddress, IntPtr dwNumBytesToFlush);

	[DllImport("kernel32", SetLastError = true)]
	public static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, int dwDesiredAccess, int dwFileOffsetHigh, int dwFileOffsetLow, IntPtr dwNumBytesToMap);

	[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr OpenFileMapping(int dwDesiredAccess, bool bInheritHandle, string lpName);
}
