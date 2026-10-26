using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;

public struct BitmapContext : IDisposable
{
	private struct BitmapContextBitmapProperties
	{
		public int Width;

		public int Height;

		public unsafe int* Pixels;

		public PixelFormat Format;

		public int BackBufferStride;
	}

	private readonly WriteableBitmap _writeableBitmap;

	private readonly ReadWriteMode _mode;

	private readonly int _pixelWidth;

	private readonly int _pixelHeight;

	private static readonly IDictionary<WriteableBitmap, int> UpdateCountByBmp = new ConcurrentDictionary<WriteableBitmap, int>();

	private static readonly IDictionary<WriteableBitmap, BitmapContextBitmapProperties> BitmapPropertiesByBmp = new ConcurrentDictionary<WriteableBitmap, BitmapContextBitmapProperties>();

	private readonly int _length;

	private unsafe readonly int* _backBuffer;

	private readonly PixelFormat _format;

	private readonly int _backBufferStride;

	public WriteableBitmap WriteableBitmap => _writeableBitmap;

	public int Width => _pixelWidth;

	public int Height => _pixelHeight;

	public unsafe int* Pixels
	{
		[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
		get
		{
			return _backBuffer;
		}
	}

	public PixelFormat Format
	{
		[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
		get
		{
			return _format;
		}
	}

	public int Length
	{
		[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
		get
		{
			return _length;
		}
	}

	public BitmapContext(WriteableBitmap writeableBitmap)
		: this(writeableBitmap, ReadWriteMode.ReadWrite)
	{
	}

	public unsafe BitmapContext(WriteableBitmap writeableBitmap, ReadWriteMode mode)
	{
		_writeableBitmap = writeableBitmap;
		_mode = mode;
		lock (UpdateCountByBmp)
		{
			BitmapContextBitmapProperties value;
			if (!UpdateCountByBmp.ContainsKey(writeableBitmap))
			{
				UpdateCountByBmp.Add(writeableBitmap, 1);
				writeableBitmap.Lock();
				value = new BitmapContextBitmapProperties
				{
					BackBufferStride = writeableBitmap.BackBufferStride,
					Pixels = (int*)(void*)writeableBitmap.BackBuffer,
					Width = writeableBitmap.PixelWidth,
					Height = writeableBitmap.PixelHeight,
					Format = writeableBitmap.Format
				};
				BitmapPropertiesByBmp.Add(writeableBitmap, value);
			}
			else
			{
				IncrementRefCount(writeableBitmap);
				value = BitmapPropertiesByBmp[writeableBitmap];
			}
			_backBufferStride = value.BackBufferStride;
			_pixelWidth = value.Width;
			_pixelHeight = value.Height;
			_format = value.Format;
			_backBuffer = value.Pixels;
			double num = _backBufferStride / 4;
			_length = (int)(num * (double)_pixelHeight);
		}
	}

	[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
	public unsafe static void BlockCopy(BitmapContext src, int srcOffset, BitmapContext dest, int destOffset, int count)
	{
		NativeMethods.CopyUnmanagedMemory((byte*)src.Pixels, srcOffset, (byte*)dest.Pixels, destOffset, count);
	}

	[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
	public unsafe static void BlockCopy(int[] src, int srcOffset, BitmapContext dest, int destOffset, int count)
	{
		fixed (int* srcPtr = src)
		{
			NativeMethods.CopyUnmanagedMemory((byte*)srcPtr, srcOffset, (byte*)dest.Pixels, destOffset, count);
		}
	}

	[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
	public unsafe static void BlockCopy(byte[] src, int srcOffset, BitmapContext dest, int destOffset, int count)
	{
		fixed (byte* srcPtr = src)
		{
			NativeMethods.CopyUnmanagedMemory(srcPtr, srcOffset, (byte*)dest.Pixels, destOffset, count);
		}
	}

	[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
	public unsafe static void BlockCopy(BitmapContext src, int srcOffset, byte[] dest, int destOffset, int count)
	{
		fixed (byte* dstPtr = dest)
		{
			NativeMethods.CopyUnmanagedMemory((byte*)src.Pixels, srcOffset, dstPtr, destOffset, count);
		}
	}

	[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
	public unsafe static void BlockCopy(BitmapContext src, int srcOffset, int[] dest, int destOffset, int count)
	{
		fixed (int* dstPtr = dest)
		{
			NativeMethods.CopyUnmanagedMemory((byte*)src.Pixels, srcOffset, (byte*)dstPtr, destOffset, count);
		}
	}

	[TargetedPatchingOptOut("Candidate for inlining across NGen boundaries for performance reasons")]
	public unsafe void Clear()
	{
		NativeMethods.SetUnmanagedMemory((IntPtr)_backBuffer, 0, _backBufferStride * _pixelHeight);
	}

	public void Dispose()
	{
		if (DecrementRefCount(_writeableBitmap) == 0)
		{
			UpdateCountByBmp.Remove(_writeableBitmap);
			BitmapPropertiesByBmp.Remove(_writeableBitmap);
			if (_mode == ReadWriteMode.ReadWrite)
			{
				_writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, _pixelWidth, _pixelHeight));
			}
			_writeableBitmap.Unlock();
		}
	}

	private static void IncrementRefCount(WriteableBitmap target)
	{
		UpdateCountByBmp[target]++;
	}

	private static int DecrementRefCount(WriteableBitmap target)
	{
		if (!UpdateCountByBmp.TryGetValue(target, out var value))
		{
			return -1;
		}
		value--;
		UpdateCountByBmp[target] = value;
		return value;
	}
}
