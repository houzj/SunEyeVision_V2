using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;

public static class MemoryHelper
{
	public enum MapAccess
	{
		FileMapCopy = 1,
		FileMapWrite = 2,
		FileMapRead = 4,
		FileMapAllAccess = 31
	}

	public static bool Is64 = Environment.Is64BitProcess;

	public static int imageBufferOffset = 152;

	public static IntPtr CreateFile(string lpFileName, int dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile)
	{
		return MemoryNativeMethods.CreateFile(lpFileName, dwDesiredAccess, dwShareMode, lpSecurityAttributes, dwCreationDisposition, dwFlagsAndAttributes, hTemplateFile);
	}

	public static IntPtr CreateFileMapping(IntPtr hFile, IntPtr lpAttributes, object flProtect, object dwMaximumSizeLow, object dwMaximumSizeHigh, string lpName)
	{
		if (Is64)
		{
			return MemoryNativeMethods.CreateFileMapping(hFile, lpAttributes, Convert.ToInt64(flProtect), Convert.ToInt64(dwMaximumSizeLow), Convert.ToInt64(dwMaximumSizeHigh), lpName);
		}
		return MemoryNativeMethods.CreateFileMapping(hFile, lpAttributes, Convert.ToInt32(flProtect), Convert.ToInt32(dwMaximumSizeLow), Convert.ToInt32(dwMaximumSizeHigh), lpName);
	}

	public static bool FlushViewOfFile(IntPtr lpBaseAddress, IntPtr dwNumBytesToFlush)
	{
		return MemoryNativeMethods.FlushViewOfFile(lpBaseAddress, dwNumBytesToFlush);
	}

	public static IntPtr MapViewOfFile(IntPtr hFileMappingObject, object dwDesiredAccess, object dwFileOffsetHigh, object dwFileOffsetLow, IntPtr dwNumBytesToMap)
	{
		if (Is64)
		{
			return MemoryNativeMethods.MapViewOfFile(hFileMappingObject, Convert.ToInt64(dwDesiredAccess), Convert.ToInt64(dwFileOffsetHigh), Convert.ToInt64(dwFileOffsetLow), dwNumBytesToMap);
		}
		return MemoryNativeMethods.MapViewOfFile(hFileMappingObject, Convert.ToInt32(dwDesiredAccess), Convert.ToInt32(dwFileOffsetHigh), Convert.ToInt32(dwFileOffsetLow), dwNumBytesToMap);
	}

	public static IntPtr OpenFileMapping(object dwDesiredAccess, bool bInheritHandle, string lpName)
	{
		if (Is64)
		{
			return MemoryNativeMethods.OpenFileMapping(Convert.ToInt64(dwDesiredAccess), bInheritHandle, lpName);
		}
		return MemoryNativeMethods.OpenFileMapping(Convert.ToInt32(dwDesiredAccess), bInheritHandle, lpName);
	}

	public static int GetLastError()
	{
		return MemoryNativeMethods.GetLastError();
	}

	public static bool UnmapViewOfFile(IntPtr lpBaseAddress)
	{
		return MemoryNativeMethods.UnmapViewOfFile(lpBaseAddress);
	}

	public static bool CloseHandle(IntPtr handle)
	{
		return MemoryNativeMethods.CloseHandle(handle);
	}

	public static object ReadFromMemory(object imageDataSize, string fileName, byte[] pBuffer, int startOffset)
	{
		if (Is64)
		{
			long value = Convert.ToInt64(imageDataSize) + imageBufferOffset - startOffset;
			IntPtr intPtr = OpenFileMapping(4L, bInheritHandle: false, fileName);
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			IntPtr intPtr2 = MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			Marshal.Copy(intPtr2 + startOffset, pBuffer, 0, Convert.ToInt32(imageDataSize));
			if (intPtr2 != IntPtr.Zero)
			{
				UnmapViewOfFile(intPtr2);
			}
			if (intPtr != IntPtr.Zero)
			{
				CloseHandle(intPtr);
			}
			return pBuffer;
		}
		int value2 = Convert.ToInt32(imageDataSize) + imageBufferOffset - startOffset;
		IntPtr intPtr3 = OpenFileMapping(4, bInheritHandle: false, fileName);
		if (intPtr3 == IntPtr.Zero)
		{
			return null;
		}
		IntPtr intPtr4 = MapViewOfFile(intPtr3, 4, 0, 0, new IntPtr(value2));
		if (intPtr4 == IntPtr.Zero)
		{
			return null;
		}
		Marshal.Copy(intPtr4 + startOffset, pBuffer, 0, Convert.ToInt32(imageDataSize));
		if (intPtr4 != IntPtr.Zero)
		{
			UnmapViewOfFile(intPtr4);
		}
		if (intPtr3 != IntPtr.Zero)
		{
			CloseHandle(intPtr3);
		}
		return pBuffer;
	}

	public static object ReadFromMemory(object imageDataSize, string fileName, byte[] pBuffer)
	{
		if (Is64)
		{
			return ReadFromMemory(imageDataSize, fileName, pBuffer, 0);
		}
		return ReadFromMemory(imageDataSize, fileName, pBuffer, 0);
	}

	public static byte[] VMReadFromMemory(string fileName, int startOffset)
	{
		if (Is64)
		{
			byte[] array = new byte[8];
			long value = array.Length;
			IntPtr intPtr = OpenFileMapping(4L, bInheritHandle: false, fileName);
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			IntPtr intPtr2 = MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			Marshal.Copy(intPtr2, array, 0, array.Length);
			if (intPtr2 != IntPtr.Zero)
			{
				UnmapViewOfFile(intPtr2);
			}
			int num = BitConverter.ToInt32(array, 0);
			if (num < 0)
			{
				return null;
			}
			byte[] array2 = new byte[num];
			value = (long)num + (long)startOffset;
			intPtr2 = MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			Marshal.Copy(intPtr2 + startOffset, array2, 0, num);
			if (intPtr2 != IntPtr.Zero)
			{
				UnmapViewOfFile(intPtr2);
			}
			if (intPtr != IntPtr.Zero)
			{
				CloseHandle(intPtr);
			}
			return array2;
		}
		byte[] array3 = new byte[8];
		int value2 = array3.Length;
		IntPtr intPtr3 = OpenFileMapping(4, bInheritHandle: false, fileName);
		if (intPtr3 == IntPtr.Zero)
		{
			return null;
		}
		IntPtr intPtr4 = MapViewOfFile(intPtr3, 4, 0, 0, new IntPtr(value2));
		if (intPtr4 == IntPtr.Zero)
		{
			return null;
		}
		Marshal.Copy(intPtr4, array3, 0, array3.Length);
		if (intPtr4 != IntPtr.Zero)
		{
			UnmapViewOfFile(intPtr4);
		}
		int num2 = BitConverter.ToInt32(array3, 0);
		if (num2 < 0)
		{
			return null;
		}
		byte[] array4 = new byte[num2];
		value2 = num2 + startOffset;
		intPtr4 = MapViewOfFile(intPtr3, 4, 0, 0, new IntPtr(value2));
		if (intPtr4 == IntPtr.Zero)
		{
			return null;
		}
		Marshal.Copy(intPtr4 + startOffset, array4, 0, num2);
		if (intPtr4 != IntPtr.Zero)
		{
			UnmapViewOfFile(intPtr4);
		}
		if (intPtr3 != IntPtr.Zero)
		{
			CloseHandle(intPtr3);
		}
		return array4;
	}

	public static object DeepCopyObject(object obj)
	{
		if (obj != null)
		{
			object obj2 = Activator.CreateInstance(obj.GetType());
			FieldInfo[] fields = obj.GetType().GetFields();
			foreach (FieldInfo fieldInfo in fields)
			{
				if (fieldInfo.FieldType.GetInterface("IList", ignoreCase: false) == null)
				{
					fieldInfo.SetValue(obj2, fieldInfo.GetValue(obj));
					continue;
				}
				IList list = (IList)fieldInfo.GetValue(obj2);
				if (list == null)
				{
					continue;
				}
				foreach (object item in (IList)fieldInfo.GetValue(obj))
				{
					list.Add(DeepCopyObject(item));
				}
			}
			return obj2;
		}
		return null;
	}
}
