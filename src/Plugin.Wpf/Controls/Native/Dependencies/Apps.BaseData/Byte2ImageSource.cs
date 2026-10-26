using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.HikExternalCall.Common;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VM.Utility;
using SunEyeVision.Plugin.Wpf.Native.Display.VMUtility;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.BaseData;

public class Byte2ImageSource
{
	public enum Interpolation
	{
		NearestNeighbor,
		Bilinear
	}

	private WriteableBitmap m_wbBitmap;

	private int m_nWidth;

	private int m_nHeight;

	private PixelFormat m_nPixelFormat = PixelFormats.Default;

	public const string PIXELFORMAT_RGB24_P3 = "Rgb24_P3";

	public static Dictionary<byte, System.Drawing.Color> ColorTable;

	public static Dictionary<int, Tuple<int, double, double>> OffsetTable;

	private const uint IMVS_PIX_MONO8 = 17301505u;

	public const uint IMVS_PIX_Coord3D_C16 = 17825976u;

	public const uint IMVS_PIX_Coord3D_ABC32f = 39846080u;

	private const uint IMVS_PIX_Coord3D_ABC32f_Planar = 39846081u;

	private const uint IMVS_PIX_Coord3D_AC32 = 2185244676u;

	private const uint IMVS_PIX_Coord3D_ABC16 = 36700345u;

	private const uint IMVS_PIX_Coord3D_ABC32 = 2187341825u;

	public static PixelFormat StringToPixelFormat(string name)
	{
		return name switch
		{
			"Gray8" => PixelFormats.Gray8, 
			"Rgb24" => PixelFormats.Rgb24, 
			"Rgb24_P3" => PixelFormats.Rgb24, 
			_ => PixelFormats.Gray8, 
		};
	}

	public void Clear()
	{
		m_wbBitmap = null;
	}

	public void InitImageSource(int nWidth, int nHeight, PixelFormat npixelFormat, BitmapPalette palette = null)
	{
		try
		{
			if (nWidth != 0 && nHeight != 0)
			{
				m_nWidth = nWidth;
				m_nHeight = nHeight;
				m_nPixelFormat = npixelFormat;
				m_wbBitmap = new WriteableBitmap(nWidth, nHeight, 96.0, 96.0, npixelFormat, palette);
			}
		}
		catch (Exception)
		{
			m_wbBitmap = null;
		}
	}

	public WriteableBitmap Init(int nWidth, int nHeight, PixelFormat npixelFormat)
	{
		try
		{
			if (nWidth != m_nWidth || nHeight != m_nHeight || npixelFormat != m_nPixelFormat || m_wbBitmap == null)
			{
				if (nWidth < 0 || nHeight < 0)
				{
					return null;
				}
				InitImageSource(nWidth, nHeight, npixelFormat);
			}
			return m_wbBitmap;
		}
		catch (Exception)
		{
			m_wbBitmap = null;
			return null;
		}
	}

	public WriteableBitmap Init(int nWidth, int nHeight, PixelFormat npixelFormat, BitmapPalette palette)
	{
		try
		{
			if (nWidth != m_nWidth || nHeight != m_nHeight || npixelFormat != m_nPixelFormat || m_wbBitmap == null)
			{
				if (nWidth < 0 || nHeight < 0)
				{
					return null;
				}
				InitImageSource(nWidth, nHeight, npixelFormat, palette);
			}
			return m_wbBitmap;
		}
		catch (Exception)
		{
			m_wbBitmap = null;
			return null;
		}
	}

	public ImageSource BuiltImageSource(byte[] rawValues, int nWidth, int nHeight, PixelFormat npixelFormats)
	{
		if (nWidth != m_nWidth || nHeight != m_nHeight || npixelFormats != m_nPixelFormat || m_wbBitmap == null)
		{
			if (nWidth < 0 || nHeight < 0)
			{
				return null;
			}
			InitImageSource(nWidth, nHeight, npixelFormats);
		}
		try
		{
			if (m_wbBitmap == null || Math.Abs((double)rawValues.Length - (double)nWidth * 1.0 * (double)nHeight * (double)m_wbBitmap.Format.BitsPerPixel / 8.0) > 0.10000000149011612)
			{
				return null;
			}
			m_wbBitmap.WritePixels(new Int32Rect(0, 0, nWidth, nHeight), rawValues, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8, 0);
		}
		catch (Exception)
		{
			return null;
		}
		return m_wbBitmap;
	}

	public ImageSource BuiltImageSourceA(IImageData image)
	{
		if (image == null)
		{
			return null;
		}
		PixelFormat npixelFormats = StringToPixelFormat(image.PixelFormat);
		if (image.Buffer == null && !string.IsNullOrEmpty(image.MemoryAddress))
		{
			return BuiltImageSource(image.MemoryAddress, image.Width, image.Height, npixelFormats);
		}
		if (image.Buffer != null)
		{
			return BuiltImageSource(image.Buffer, image.Width, image.Height, npixelFormats);
		}
		return null;
	}

	public unsafe ImageSource BuiltImageSourceA(IImageData image, int width, int height, ref bool status, int offSetXPixel = 0, int offSetYPixel = 0, int clipXPixel = 0, int clipYPixel = 0, bool isDirect = false)
	{
		if (image == null)
		{
			return null;
		}
		uint executeCount = 0u;
		if (image is IImageDataExtern imageDataExtern)
		{
			if (imageDataExtern.NeedConvertColor)
			{
				CovertToClassImageSource(image);
			}
			executeCount = (imageDataExtern.NeedCheckExecuteCount ? imageDataExtern.ExecuteCount : 0u);
		}
		PixelFormat pixelFormat = StringToPixelFormat(image.PixelFormat);
		if (image.Width < width || image.Height < height)
		{
			width = image.Width;
			height = image.Height;
		}
		double num = (double)image.Width / (double)width;
		double num2 = (double)image.Height / (double)height;
		int num3 = (int)((double)offSetXPixel / num);
		int num4 = width - num3;
		int num5 = (int)((double)offSetYPixel / num2);
		int num6 = height - num5;
		int num7 = (int)((double)clipXPixel / num);
		num4 -= num7;
		int num8 = (int)((double)clipYPixel / num2);
		num6 -= num8;
		WriteableBitmap writeableBitmap = Init(num4, num6, pixelFormat);
		if (writeableBitmap == null)
		{
			return null;
		}
		if (image.Buffer == null)
		{
			if (string.IsNullOrEmpty(image.MemoryAddress))
			{
				return null;
			}
			int num9 = ((pixelFormat == PixelFormats.Gray8) ? 1 : 3);
			if (isDirect || (long)image.Width * (long)image.Height * num9 > int.MaxValue)
			{
				if (GetImageBufferByFileNameDirectly(writeableBitmap, width, height, image.MemoryAddress, image.Width, image.Height, pixelFormat, image.PixelFormat, executeCount, ref status, offSetXPixel, offSetYPixel, clipXPixel, clipYPixel, num4, num6) == null)
				{
					return null;
				}
				return writeableBitmap;
			}
			image.Buffer = GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, pixelFormat, isDirect: false);
		}
		if (image.Buffer == null)
		{
			return null;
		}
		int num10 = ((pixelFormat == PixelFormats.Gray8) ? 1 : 3);
		if (image.Buffer.Length != image.Width * image.Height * num10)
		{
			return null;
		}
		writeableBitmap.Lock();
		fixed (byte* buffer = image.Buffer)
		{
			Resize(buffer, (byte*)(void*)writeableBitmap.BackBuffer, image.Width, image.Height, width, height, Interpolation.NearestNeighbor, pixelFormat, image.PixelFormat, offSetXPixel, offSetYPixel, clipXPixel, clipYPixel, num4, num6);
		}
		if (num4 > 0 && num6 > 0)
		{
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, num4, num6));
		}
		writeableBitmap.Unlock();
		return writeableBitmap;
	}

	private unsafe byte[] GetImageBufferByFileNameDirectly(WriteableBitmap bmp, int width, int height, string fileName, int nWidth, int nHeight, PixelFormat npixelFormats, string strPixelFormat, uint executeCount, ref bool status, int offSetXPixel = 0, int offSetYPixel = 0, int clipXPixel = 0, int clipYPixel = 0, int fwidth = 0, int fheight = 0)
	{
		int num = 16;
		try
		{
			byte[] array = new byte[16];
			_ = array.Length;
			using MemoryMappedFile memoryMappedFile = MemoryMappedFile.OpenExisting(fileName);
			using MemoryMappedViewAccessor memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor();
			memoryMappedViewAccessor.ReadArray(0L, array, 0, array.Length);
			if (array[8] != 0)
			{
				return null;
			}
			num = BitConverter.ToInt16(array, 14);
			if (num > 84 && executeCount != 0)
			{
				array = new byte[num];
				memoryMappedViewAccessor.ReadArray(0L, array, 0, array.Length);
				if (BitConverter.ToUInt32(array, 80) != executeCount)
				{
					status = false;
					return null;
				}
			}
			long num2 = BitConverter.ToInt64(array, 0);
			if (num2 < 0)
			{
				return null;
			}
			if (npixelFormats == PixelFormats.Gray8)
			{
				if (num2 != (long)nWidth * (long)nHeight)
				{
					return null;
				}
			}
			else if (num2 != (long)nWidth * (long)nHeight * 3)
			{
				return null;
			}
			IntPtr intPtr = memoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			if (m_wbBitmap == null)
			{
				return null;
			}
			if (num2 != (long)nWidth * (long)nHeight * (m_wbBitmap.Format.BitsPerPixel / 8))
			{
				return null;
			}
			byte* pixels = (byte*)(intPtr + num).ToPointer();
			bmp.Lock();
			Resize(pixels, (byte*)(void*)bmp.BackBuffer, nWidth, nHeight, width, height, Interpolation.NearestNeighbor, npixelFormats, strPixelFormat, offSetXPixel, offSetYPixel, clipXPixel, clipYPixel, fwidth, fheight);
			if (fwidth > 0 && fheight > 0)
			{
				bmp.AddDirtyRect(new Int32Rect(0, 0, fwidth, fheight));
			}
			bmp.Unlock();
			return new byte[1];
		}
		catch (Exception)
		{
			return null;
		}
	}

	public unsafe ImageSource BuiltBgraImageSource(IImageData image, int width, int height, byte transparency)
	{
		if (image == null)
		{
			return null;
		}
		if (image.Width < width || image.Height < height)
		{
			double num = (double)width / (double)height;
			double num2 = (double)image.Width / (double)image.Height;
			if (num > num2)
			{
				width = image.Width;
				height = (int)((double)width / num);
			}
			else
			{
				height = image.Height;
				width = (int)(num * (double)height);
			}
		}
		WriteableBitmap writeableBitmap = Init(width, height, PixelFormats.Bgra32);
		if (writeableBitmap == null)
		{
			return null;
		}
		PixelFormat pixelFormat = StringToPixelFormat(image.PixelFormat);
		if (image.Buffer == null)
		{
			if (string.IsNullOrEmpty(image.MemoryAddress))
			{
				return null;
			}
			image.Buffer = GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, pixelFormat);
		}
		if (image.Buffer == null)
		{
			return null;
		}
		int num3 = ((pixelFormat == PixelFormats.Gray8) ? 1 : 3);
		if (image.Buffer.Length != image.Width * image.Height * num3)
		{
			return null;
		}
		bool needConvertColor = (image as IImageDataExtern)?.NeedConvertColor ?? false;
		writeableBitmap.Lock();
		fixed (byte* buffer = image.Buffer)
		{
			ResizeToBGRA(buffer, (byte*)(void*)writeableBitmap.BackBuffer, image.Width, image.Height, width, height, Interpolation.NearestNeighbor, pixelFormat, image.PixelFormat, needConvertColor, transparency);
		}
		writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
		writeableBitmap.Unlock();
		return writeableBitmap;
	}

	private unsafe void ResizeToBGRA(byte* pixels, byte* bufferptr, int widthSource, int heightSource, int width, int height, Interpolation interpolation, PixelFormat pixelFormat, string strPixelFormat, bool needConvertColor, byte transparency)
	{
		bool flag = strPixelFormat != "Rgb24_P3";
		int channelSource = pixelFormat.BitsPerPixel / 8;
		int channel = PixelFormats.Bgra32.BitsPerPixel / 8;
		int strideSource = widthSource * channelSource;
		int stride = (int)Math.Ceiling((double)width * (double)PixelFormats.Bgra32.BitsPerPixel / 32.0) * 4;
		double xs = (double)widthSource / (double)width;
		double ys = (double)heightSource / (double)height;
		int inc1 = 0;
		int inc2 = 1;
		int inc3 = 2;
		if (pixelFormat == PixelFormats.Gray8)
		{
			inc1 = 0;
			inc2 = 0;
			inc3 = 0;
		}
		if (interpolation != Interpolation.NearestNeighbor)
		{
			return;
		}
		int dlen = width * channel;
		if (flag)
		{
			Parallel.For(0, height, (int y) =>
			{
				byte* ptr = pixels + (int)(ys * (double)y) * strideSource;
				byte* ptr2 = bufferptr + y * stride;
				double num2 = 0.0;
				byte* ptr3 = dlen + ptr2;
				for (byte* ptr4 = ptr2; ptr4 < ptr3; ptr4 += channel)
				{
					byte* ptr5 = ptr + (int)num2 * channelSource;
					num2 += xs;
					byte b = *ptr5;
					byte b2 = transparency;
					if (pixelFormat == PixelFormats.Gray8)
					{
						if (b == 0)
						{
							b2 = 0;
						}
						else if (needConvertColor)
						{
							System.Drawing.Color categoryColor = GetCategoryColor(b);
							*ptr4 = categoryColor.B;
							ptr4[1] = categoryColor.G;
							ptr4[2] = categoryColor.R;
							ptr4[3] = b2;
							continue;
						}
					}
					*ptr4 = ptr5[inc3];
					ptr4[1] = ptr5[inc2];
					ptr4[2] = ptr5[inc1];
					ptr4[3] = b2;
				}
			});
			return;
		}
		int num = widthSource * heightSource;
		byte* pixels_offx_channel_R = pixels;
		byte* pixels_offx_channel_G = pixels + num;
		byte* pixels_offx_channel_B = pixels + num * 2;
		Parallel.For(0, height, (int y) =>
		{
			int num2 = (int)(ys * (double)y) * widthSource;
			byte* ptr = pixels_offx_channel_R + num2;
			byte* ptr2 = pixels_offx_channel_G + num2;
			byte* ptr3 = pixels_offx_channel_B + num2;
			byte* ptr4 = bufferptr + y * stride;
			double num3 = 0.0;
			byte* ptr5 = dlen + ptr4;
			for (byte* ptr6 = ptr4; ptr6 < ptr5; ptr6 += channel)
			{
				byte* ptr7 = ptr + (int)num3;
				byte* ptr8 = ptr2 + (int)num3;
				byte* ptr9 = ptr3 + (int)num3;
				num3 += xs;
				*ptr6 = *ptr9;
				ptr6[1] = *ptr8;
				ptr6[2] = *ptr7;
				ptr6[3] = transparency;
			}
		});
	}

	public void CovertToClassImageSource(IImageData image)
	{
		if (image == null)
		{
			return;
		}
		PixelFormat pixelFormat = StringToPixelFormat(image.PixelFormat);
		if (!(pixelFormat == PixelFormats.Gray8))
		{
			return;
		}
		if (image.Buffer == null)
		{
			if (string.IsNullOrEmpty(image.MemoryAddress))
			{
				return;
			}
			image.Buffer = GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, pixelFormat);
		}
		if (image.Buffer == null || image.Buffer.Length != image.Width * image.Height)
		{
			return;
		}
		int bufferStride = image.Width * image.Height;
		int num = bufferStride * 3;
		byte[] newBuffer = new byte[num];
		int toExclusive = (int)Math.Ceiling((double)bufferStride / 5000.0);
		Parallel.For(0, toExclusive, (int i) =>
		{
			int num2 = i * 5000;
			int num3 = num2 + 5000;
			for (int j = num2; j < num3 && j < bufferStride; j++)
			{
				byte b = image.Buffer[j];
				if (b != 0)
				{
					System.Drawing.Color categoryColor = GetCategoryColor(b);
					int num4 = 3 * j;
					newBuffer[num4] = categoryColor.B;
					newBuffer[num4 + 1] = categoryColor.G;
					newBuffer[num4 + 2] = categoryColor.R;
				}
			}
		});
		image.Buffer = newBuffer;
		if (image is IImageDataExtern imageDataExtern)
		{
			imageDataExtern.SetPixelFormat("Rgb24");
		}
	}

	public int GetClassImageGrayVal(System.Drawing.Color color)
	{
		foreach (KeyValuePair<byte, System.Drawing.Color> item in ColorTable)
		{
			if (item.Value == color)
			{
				return item.Key;
			}
		}
		return -1;
	}

	public unsafe ImageSource BuiltRgbImageSource(IImageData image, int width = 0, int height = 0)
	{
		if (image == null)
		{
			return null;
		}
		PixelFormat pixelFormat = StringToPixelFormat(image.PixelFormat);
		if (image.Buffer == null)
		{
			if (string.IsNullOrEmpty(image.MemoryAddress))
			{
				return null;
			}
			image.Buffer = GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, pixelFormat);
		}
		if (image.Buffer == null)
		{
			return null;
		}
		int num = ((pixelFormat == PixelFormats.Gray8) ? 1 : 3);
		if (image.Buffer.Length != image.Width * image.Height * num)
		{
			return null;
		}
		int num2 = image.Width * image.Height;
		int num3 = num2 * 3;
		byte[] newBuffer = null;
		if (pixelFormat == PixelFormats.Gray8)
		{
			newBuffer = new byte[num3];
			try
			{
				Parallel.For(0, num2, (int i) =>
				{
					byte b = image.Buffer[i];
					if (b != 0)
					{
						System.Drawing.Color categoryColor = GetCategoryColor(b);
						int num4 = 3 * i;
						newBuffer[num4] = categoryColor.B;
						newBuffer[num4 + 1] = categoryColor.G;
						newBuffer[num4 + 2] = categoryColor.R;
					}
				});
			}
			catch
			{
			}
		}
		else
		{
			newBuffer = image.Buffer;
		}
		if (height == 0 || width == 0)
		{
			return BuiltImageSource(newBuffer, image.Width, image.Height, PixelFormats.Rgb24);
		}
		WriteableBitmap writeableBitmap = Init(width, height, PixelFormats.Rgb24);
		if (writeableBitmap == null)
		{
			return null;
		}
		writeableBitmap.Lock();
		fixed (byte* pixels = newBuffer)
		{
			ResizeA(pixels, (byte*)(void*)writeableBitmap.BackBuffer, image.Width, image.Height, width, height, Interpolation.NearestNeighbor, PixelFormats.Rgb24);
		}
		writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
		writeableBitmap.Unlock();
		return writeableBitmap;
	}

	public unsafe static void ResizeA(byte* pixels, byte* bufferptr, int widthSource, int heightSource, int width, int height, Interpolation interpolation, PixelFormat pixelFormat)
	{
		int channel = pixelFormat.BitsPerPixel / 8;
		int strideSource = widthSource * pixelFormat.BitsPerPixel / 8;
		int stride = (int)Math.Ceiling((double)width * (double)pixelFormat.BitsPerPixel / 32.0) * 4;
		float xs = (float)widthSource / (float)width;
		float ys = (float)heightSource / (float)height;
		if (interpolation != Interpolation.NearestNeighbor)
		{
			return;
		}
		_ = stride;
		_ = bufferptr;
		int dlen = width * channel;
		Parallel.For(0, height, (int y) =>
		{
			byte* ptr = pixels + (int)(ys * (float)y) * strideSource;
			byte* ptr2 = bufferptr + y * stride;
			double num = 0.0;
			byte* ptr3 = dlen + ptr2;
			for (byte* ptr4 = ptr2; ptr4 < ptr3; ptr4 += channel)
			{
				byte* ptr5 = ptr + (int)num * channel;
				num += (double)xs;
				for (int i = 0; i < channel; i++)
				{
					ptr4[i] = ptr5[i];
				}
			}
		});
	}

	private System.Drawing.Color GetCategoryColor(byte mono)
	{
		if (ColorTable.TryGetValue(mono, out var value))
		{
			return value;
		}
		return System.Drawing.Color.FromArgb(255, mono, mono, mono);
	}

	static Byte2ImageSource()
	{
		ColorTable = new Dictionary<byte, System.Drawing.Color>();
		OffsetTable = new Dictionary<int, Tuple<int, double, double>>
		{
			{
				0,
				new Tuple<int, double, double>(0, 1.0, 1.0)
			},
			{
				1,
				new Tuple<int, double, double>(0, 1.0, 0.5)
			},
			{
				2,
				new Tuple<int, double, double>(35, 0.5, 1.0)
			},
			{
				3,
				new Tuple<int, double, double>(35, 0.5, 1.0)
			},
			{
				4,
				new Tuple<int, double, double>(17, 0.5, 0.5)
			},
			{
				5,
				new Tuple<int, double, double>(17, 0.5, 0.5)
			},
			{
				6,
				new Tuple<int, double, double>(53, 0.5, 0.5)
			},
			{
				7,
				new Tuple<int, double, double>(53, 0.5, 0.5)
			}
		};
		for (byte b = 0; b < 4; b++)
		{
			for (byte b2 = 0; b2 < 8; b2++)
			{
				ColorTable.Add((byte)(b2 * 4 + b + 1), ColorFromHSV(b * 70 + OffsetTable[b2].Item1 + 60, OffsetTable[b2].Item2, OffsetTable[b2].Item3));
			}
		}
		ColorTable.Add(0, System.Drawing.Color.Black);
		ColorTable.Add(byte.MaxValue, System.Drawing.Color.FromArgb(255, 0, 255, 0));
	}

	public static System.Drawing.Color ColorFromHSV(double hue, double saturation, double value)
	{
		int num = Convert.ToInt32(Math.Floor(hue / 60.0)) % 6;
		double num2 = hue / 60.0 - Math.Floor(hue / 60.0);
		value *= 255.0;
		int num3 = Convert.ToInt32(value);
		int num4 = Convert.ToInt32(value * (1.0 - saturation));
		int num5 = Convert.ToInt32(value * (1.0 - num2 * saturation));
		int num6 = Convert.ToInt32(value * (1.0 - (1.0 - num2) * saturation));
		return num switch
		{
			0 => System.Drawing.Color.FromArgb(255, num3, num6, num4), 
			1 => System.Drawing.Color.FromArgb(255, num5, num3, num4), 
			2 => System.Drawing.Color.FromArgb(255, num4, num3, num6), 
			3 => System.Drawing.Color.FromArgb(255, num4, num5, num3), 
			4 => System.Drawing.Color.FromArgb(255, num6, num4, num3), 
			_ => System.Drawing.Color.FromArgb(255, num3, num4, num5), 
		};
	}

	[DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "memcpy")]
	public unsafe static extern void Memcpy(byte* dest, byte* src, int count);

	public unsafe static void Resize(byte* pixels, byte* bufferptr, int widthSource, int heightSource, int width, int height, Interpolation interpolation, PixelFormat pixelFormat, string strPixelFormat, int offx = 0, int offy = 0, int clipXPixel = 0, int clipYPixel = 0, int fwidth = 0, int fheight = 0)
	{
		bool flag = strPixelFormat != "Rgb24_P3";
		int channel = pixelFormat.BitsPerPixel / 8;
		int strideSource = widthSource * channel;
		int stride = (int)Math.Ceiling((double)fwidth * (double)pixelFormat.BitsPerPixel / 32.0) * 4;
		double xs = (double)widthSource / (double)width;
		double ys = (double)heightSource / (double)height;
		if (interpolation != Interpolation.NearestNeighbor)
		{
			return;
		}
		int dlen = fwidth * channel;
		offx = (int)(Math.Floor((double)offx / xs) * xs);
		offy = (int)(Math.Floor((double)offy / ys) * ys);
		if (flag)
		{
			byte* pixels_offx_channel = pixels + offx * channel + offy * strideSource;
			if (xs == 1.0)
			{
				Parallel.For(0, fheight, (int y) =>
				{
					byte* src = pixels_offx_channel + (int)(ys * (double)y) * strideSource;
					Memcpy(bufferptr + y * stride, src, dlen);
				});
				return;
			}
			Parallel.For(0, fheight, (int y) =>
			{
				byte* ptr = pixels_offx_channel + (int)(ys * (double)y) * strideSource;
				byte* ptr2 = bufferptr + y * stride;
				double num2 = 0.0;
				byte* ptr3 = dlen + ptr2;
				for (byte* ptr4 = ptr2; ptr4 < ptr3; ptr4 += channel)
				{
					byte* ptr5 = ptr + (int)num2 * channel;
					num2 += xs;
					for (int i = 0; i < channel; i++)
					{
						ptr4[i] = ptr5[i];
					}
				}
			});
			return;
		}
		int num = widthSource * heightSource;
		byte* pixels_offx_channel_R = pixels + offx + offy * widthSource;
		byte* pixels_offx_channel_G = pixels + num + offx + offy * widthSource;
		byte* pixels_offx_channel_B = pixels + num * 2 + offx + offy * widthSource;
		Parallel.For(0, fheight, (int y) =>
		{
			int num2 = (int)(ys * (double)y) * widthSource;
			byte* ptr = pixels_offx_channel_R + num2;
			byte* ptr2 = pixels_offx_channel_G + num2;
			byte* ptr3 = pixels_offx_channel_B + num2;
			byte* ptr4 = bufferptr + y * stride;
			double num3 = 0.0;
			byte* ptr5 = dlen + ptr4;
			for (byte* ptr6 = ptr4; ptr6 < ptr5; ptr6 += channel)
			{
				byte* ptr7 = ptr + (int)num3;
				byte* ptr8 = ptr2 + (int)num3;
				byte* ptr9 = ptr3 + (int)num3;
				num3 += xs;
				*ptr6 = *ptr7;
				ptr6[1] = *ptr8;
				ptr6[2] = *ptr9;
			}
		});
	}

	public ImageSource BuiltImageSource(IntPtr imageData, int dataLenth, int nWidth, int nHeight, PixelFormat npixelFormats)
	{
		if (nWidth != m_nWidth || nHeight != m_nHeight || npixelFormats != m_nPixelFormat || m_wbBitmap == null)
		{
			if (nWidth < 0 || nHeight < 0)
			{
				return null;
			}
			InitImageSource(nWidth, nHeight, npixelFormats);
		}
		try
		{
			if (m_wbBitmap != null)
			{
				m_wbBitmap.WritePixels(new Int32Rect(0, 0, nWidth, nHeight), imageData, dataLenth, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8);
			}
		}
		catch
		{
		}
		return m_wbBitmap;
	}

	public ImageSource BuiltImageSource(string fileName, int nWidth, int nHeight, PixelFormat npixelFormats)
	{
		if (nWidth != m_nWidth || nHeight != m_nHeight || npixelFormats != m_nPixelFormat || m_wbBitmap == null)
		{
			if (nWidth < 0 || nHeight < 0)
			{
				return null;
			}
			InitImageSource(nWidth, nHeight, npixelFormats);
		}
		GetImageBufferByFileName(fileName, nWidth, nHeight, npixelFormats, true);
		return m_wbBitmap;
	}

	public byte[] GetImageBufferByFileName(string fileName, int nWidth, int nHeight, PixelFormat npixelFormats, bool isDirect = true)
	{
		int num = 16;
		try
		{
			if (MemoryHelper.Is64)
			{
				byte[] array = new byte[16];
				_ = array.Length;
				using MemoryMappedFile memoryMappedFile = MemoryMappedFile.OpenExisting(fileName);
				using MemoryMappedViewAccessor memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor();
				memoryMappedViewAccessor.ReadArray(0L, array, 0, array.Length);
				if (array[8] != 0)
				{
					return null;
				}
				long num2 = BitConverter.ToInt64(array, 0);
				if (num2 < 0)
				{
					return null;
				}
				if (npixelFormats == PixelFormats.Gray8)
				{
					if (num2 != (long)nWidth * (long)nHeight)
					{
						return null;
					}
				}
				else if (num2 != (long)nWidth * (long)nHeight * 3)
				{
					return null;
				}
				num = BitConverter.ToInt16(array, 14);
				IntPtr intPtr = memoryMappedViewAccessor.SafeMemoryMappedViewHandle.DangerousGetHandle();
				if (intPtr == IntPtr.Zero)
				{
					return null;
				}
				byte[] result = null;
				if (isDirect)
				{
					if (m_wbBitmap == null)
					{
						return null;
					}
					if (num2 != (long)nWidth * (long)nHeight * (m_wbBitmap.Format.BitsPerPixel / 8))
					{
						return null;
					}
					m_wbBitmap.WritePixels(new Int32Rect(0, 0, nWidth, nHeight), intPtr + num, (int)num2, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8);
				}
				else
				{
					result = ExternalCallHelper.IntPtr2Bytes(intPtr + num, (int)num2);
				}
				return result;
			}
			byte[] array2 = new byte[16];
			_ = array2.Length;
			using MemoryMappedFile memoryMappedFile2 = MemoryMappedFile.OpenExisting(fileName);
			using MemoryMappedViewAccessor memoryMappedViewAccessor2 = memoryMappedFile2.CreateViewAccessor();
			memoryMappedViewAccessor2.ReadArray(0L, array2, 0, array2.Length);
			if (array2[8] != 0)
			{
				return null;
			}
			long num3 = BitConverter.ToInt64(array2, 0);
			if (num3 < 0)
			{
				return null;
			}
			if (npixelFormats == PixelFormats.Gray8)
			{
				if (num3 != (long)nWidth * (long)nHeight)
				{
					return null;
				}
			}
			else if (num3 != (long)nWidth * (long)nHeight * 3)
			{
				return null;
			}
			num = BitConverter.ToInt16(array2, 14);
			IntPtr intPtr2 = memoryMappedViewAccessor2.SafeMemoryMappedViewHandle.DangerousGetHandle();
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			byte[] result2 = null;
			if (isDirect)
			{
				if (m_wbBitmap == null)
				{
					return null;
				}
				if (num3 != nWidth * nHeight * (m_wbBitmap.Format.BitsPerPixel / 8))
				{
					return null;
				}
				m_wbBitmap.WritePixels(new Int32Rect(0, 0, nWidth, nHeight), intPtr2 + num, (int)num3, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8);
			}
			else
			{
				result2 = ExternalCallHelper.IntPtr2Bytes(intPtr2 + num, (int)num3);
			}
			return result2;
		}
		catch
		{
			return null;
		}
	}

	public uint GetExcuteCountFromFileName(string fileName)
	{
		uint result = 0u;
		int num = 16;
		try
		{
			byte[] array = new byte[16];
			_ = array.Length;
			using MemoryMappedFile memoryMappedFile = MemoryMappedFile.OpenExisting(fileName);
			using MemoryMappedViewAccessor memoryMappedViewAccessor = memoryMappedFile.CreateViewAccessor();
			memoryMappedViewAccessor.ReadArray(0L, array, 0, array.Length);
			if (array[8] != 0)
			{
				return 0u;
			}
			num = BitConverter.ToInt16(array, 14);
			if (num > 84)
			{
				array = new byte[num];
				memoryMappedViewAccessor.ReadArray(0L, array, 0, array.Length);
				result = BitConverter.ToUInt32(array, 80);
			}
		}
		catch
		{
			return 0u;
		}
		return result;
	}

	public static byte[] GetImageBufferByFileName(string fileName, int nWidth, int nHeight, PixelFormat npixelFormats)
	{
		int num = 16;
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		try
		{
			if (MemoryHelper.Is64)
			{
				byte[] array = new byte[16];
				long value = array.Length;
				intPtr = MemoryHelper.OpenFileMapping(4L, bInheritHandle: false, fileName);
				if (intPtr == IntPtr.Zero)
				{
					return null;
				}
				intPtr2 = MemoryHelper.MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
				if (intPtr2 == IntPtr.Zero)
				{
					return null;
				}
				Marshal.Copy(intPtr2, array, 0, array.Length);
				if (intPtr2 != IntPtr.Zero)
				{
					MemoryHelper.UnmapViewOfFile(intPtr2);
				}
				if (array[8] != 0)
				{
					return null;
				}
				long num2 = BitConverter.ToInt64(array, 0);
				if (num2 < 0)
				{
					return null;
				}
				if (npixelFormats == PixelFormats.Gray8)
				{
					if (num2 != (long)nWidth * (long)nHeight)
					{
						return null;
					}
				}
				else if (num2 != (long)nWidth * (long)nHeight * 3)
				{
					return null;
				}
				num = BitConverter.ToInt16(array, 14);
				value = num2 + num;
				intPtr2 = MemoryHelper.MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
				if (intPtr2 == IntPtr.Zero)
				{
					return null;
				}
				return ExternalCallHelper.IntPtr2Bytes(size: (int)((!(npixelFormats == PixelFormats.Gray8)) ? ((long)nWidth * (long)nHeight * 3) : ((long)nWidth * (long)nHeight)), ptr: intPtr2 + num);
			}
		}
		catch
		{
			return null;
		}
		finally
		{
			if (intPtr2 != IntPtr.Zero)
			{
				MemoryHelper.UnmapViewOfFile(intPtr2);
			}
			if (intPtr != IntPtr.Zero)
			{
				MemoryHelper.CloseHandle(intPtr);
			}
		}
		return null;
	}

	public ImageSource BuiltImageSource(IImageData image)
	{
		PixelFormat pixelFormat = StringToPixelFormat(image.PixelFormat);
		int num = ((pixelFormat == PixelFormats.Gray8) ? 1 : 3);
		if (image.Buffer.Length != image.Width * image.Height * num)
		{
			return null;
		}
		if (image.Width != m_nWidth || image.Height != m_nHeight || pixelFormat != m_nPixelFormat || m_wbBitmap == null)
		{
			if (image.Width < 0 || image.Height < 0)
			{
				return null;
			}
			InitImageSource(image.Width, image.Height, pixelFormat);
		}
		try
		{
			if (m_wbBitmap != null)
			{
				m_wbBitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), image.Buffer, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8, 0);
			}
		}
		catch (Exception)
		{
			return null;
		}
		return m_wbBitmap;
	}

	public static bool IsCoord3D_ABC16(uint format)
	{
		return format == 36700345;
	}

	public static bool IsCoord3D_ABC32(uint format)
	{
		return format == 2187341825u;
	}

	public unsafe ImageSource BuiltImageSourceA(IImageData image, int width, int height, bool isAuto, int min, int max, int offSetXPixel = 0, int offSetYPixel = 0, int clipXPixel = 0, int clipYPixel = 0, bool isDirect = false)
	{
		if (image == null)
		{
			return null;
		}
		if (image is IImageDataExtern { NeedConvertColor: not false })
		{
			CovertToClassImageSource(image);
		}
		PixelFormat pixelFormat = StringToPixelFormat(image.PixelFormat);
		if (image.Width < width || image.Height < height)
		{
			width = image.Width;
			height = image.Height;
		}
		double num = (double)image.Width / (double)width;
		double num2 = (double)image.Height / (double)height;
		bool status = true;
		int num3 = (int)((double)offSetXPixel / num);
		int num4 = width - num3;
		int num5 = (int)((double)offSetYPixel / num2);
		int num6 = height - num5;
		int num7 = (int)((double)clipXPixel / num);
		num4 -= num7;
		int num8 = (int)((double)clipYPixel / num2);
		num6 -= num8;
		WriteableBitmap writeableBitmap = Init(num4, num6, pixelFormat);
		if (writeableBitmap == null)
		{
			return null;
		}
		if (image.Buffer == null)
		{
			if (string.IsNullOrEmpty(image.MemoryAddress))
			{
				return null;
			}
			if (isDirect)
			{
				if (GetImageBufferByFileNameDirectly(writeableBitmap, width, height, image.MemoryAddress, image.Width, image.Height, pixelFormat, image.PixelFormat, 0u, ref status, offSetXPixel, offSetYPixel, clipXPixel, clipYPixel, num4, num6) == null)
				{
					return null;
				}
				return writeableBitmap;
			}
			image.Buffer = GetImageBufferByFileName(image.MemoryAddress, image.Width, image.Height, pixelFormat, (image as IRangeImageData)?.DetailedPixelFormat, isDirect: false);
		}
		if (image.Buffer == null)
		{
			return null;
		}
		if (image is IRangeImageData { DetailedPixelFormat: 17825976u } rangeImageData)
		{
			byte[] array = new byte[image.Width * image.Height];
			if (rangeImageData.RangeBuffer == null)
			{
				rangeImageData.RangeBuffer = image.Buffer;
			}
			if (!ConvertMono16ToMono8(rangeImageData.RangeBuffer, image.Width, image.Height, array, isAuto, min, max))
			{
				return null;
			}
			image.Buffer = array;
		}
		int num9 = ((pixelFormat == PixelFormats.Gray8) ? 1 : 3);
		if (image.Buffer.Length != image.Width * image.Height * num9)
		{
			return null;
		}
		writeableBitmap.Lock();
		fixed (byte* buffer = image.Buffer)
		{
			Resize(buffer, (byte*)(void*)writeableBitmap.BackBuffer, image.Width, image.Height, width, height, Interpolation.NearestNeighbor, pixelFormat, image.PixelFormat, offSetXPixel, offSetYPixel, clipXPixel, clipYPixel, num4, num6);
		}
		if (num4 > 0 && num6 > 0)
		{
			writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, num4, num6));
		}
		writeableBitmap.Unlock();
		return writeableBitmap;
	}

	public byte[] GetImageBufferByFileName(string fileName, int nWidth, int nHeight, PixelFormat npixelFormats, uint? nDetailedPixelFormat, bool isDirect = true)
	{
		int num = 16;
		IntPtr intPtr = IntPtr.Zero;
		IntPtr intPtr2 = IntPtr.Zero;
		try
		{
			if (MemoryHelper.Is64)
			{
				byte[] array = new byte[16];
				long value = array.Length;
				intPtr = MemoryHelper.OpenFileMapping(4L, bInheritHandle: false, fileName);
				if (intPtr == IntPtr.Zero)
				{
					return null;
				}
				intPtr2 = MemoryHelper.MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
				if (intPtr2 == IntPtr.Zero)
				{
					return null;
				}
				Marshal.Copy(intPtr2, array, 0, array.Length);
				if (intPtr2 != IntPtr.Zero)
				{
					MemoryHelper.UnmapViewOfFile(intPtr2);
				}
				if (array[8] != 0)
				{
					return null;
				}
				int num2 = BitConverter.ToInt32(array, 0);
				if (num2 < 0)
				{
					return null;
				}
				if (npixelFormats == PixelFormats.Gray8)
				{
					int num3 = nWidth * nHeight;
					if (nDetailedPixelFormat.HasValue && nDetailedPixelFormat == 17825976)
					{
						num3 = nWidth * nHeight * 2;
					}
					if (nDetailedPixelFormat.HasValue && nDetailedPixelFormat == 36700345)
					{
						num3 = nWidth * nHeight * 2 * 3;
					}
					if (nDetailedPixelFormat.HasValue && (nDetailedPixelFormat == 39846080 || nDetailedPixelFormat == 39846081 || nDetailedPixelFormat == (uint?)2185244676u || nDetailedPixelFormat == (uint?)2187341825u))
					{
						num3 = nWidth * nHeight * 4 * 3;
					}
					if (num2 != num3)
					{
						return null;
					}
				}
				else if (num2 != nWidth * nHeight * 3)
				{
					return null;
				}
				num = BitConverter.ToInt16(array, 14);
				value = (long)num2 + (long)num;
				intPtr2 = MemoryHelper.MapViewOfFile(intPtr, 4L, 0, 0, new IntPtr(value));
				if (intPtr2 == IntPtr.Zero)
				{
					return null;
				}
				byte[] result = null;
				if (isDirect)
				{
					if (m_wbBitmap == null)
					{
						return null;
					}
					if (num2 != nWidth * nHeight * (m_wbBitmap.Format.BitsPerPixel / 8))
					{
						return null;
					}
					m_wbBitmap.WritePixels(new Int32Rect(0, 0, nWidth, nHeight), intPtr2 + num, num2, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8);
				}
				else
				{
					result = ExternalCallHelper.IntPtr2Bytes(intPtr2 + num, num2);
				}
				return result;
			}
			byte[] array2 = new byte[9];
			int value2 = array2.Length;
			intPtr = MemoryHelper.OpenFileMapping(4, bInheritHandle: false, fileName);
			if (intPtr == IntPtr.Zero)
			{
				return null;
			}
			intPtr2 = MemoryHelper.MapViewOfFile(intPtr, 4, 0, 0, new IntPtr(value2));
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			Marshal.Copy(intPtr2, array2, 0, array2.Length);
			if (intPtr2 != IntPtr.Zero)
			{
				MemoryHelper.UnmapViewOfFile(intPtr2);
			}
			if (array2[8] != 0)
			{
				return null;
			}
			int num4 = BitConverter.ToInt32(array2, 0);
			if (num4 < 0)
			{
				return null;
			}
			if (npixelFormats == PixelFormats.Gray8)
			{
				int num5 = nWidth * nHeight;
				if (nDetailedPixelFormat.HasValue && nDetailedPixelFormat == 17825976)
				{
					num5 = nWidth * nHeight * 2;
				}
				if (nDetailedPixelFormat.HasValue && nDetailedPixelFormat == 36700345)
				{
					num5 = nWidth * nHeight * 2 * 3;
				}
				if (nDetailedPixelFormat.HasValue && (nDetailedPixelFormat == 39846080 || nDetailedPixelFormat == 39846081 || nDetailedPixelFormat == (uint?)2185244676u || nDetailedPixelFormat == (uint?)2187341825u))
				{
					num5 = nWidth * nHeight * 4 * 3;
				}
				if (num4 != num5)
				{
					return null;
				}
			}
			else if (num4 != nWidth * nHeight * 3)
			{
				return null;
			}
			value2 = num4 + num;
			intPtr2 = MemoryHelper.MapViewOfFile(intPtr, 4, 0, 0, new IntPtr(value2));
			if (intPtr2 == IntPtr.Zero)
			{
				return null;
			}
			byte[] result2 = null;
			if (m_wbBitmap == null)
			{
				return null;
			}
			if (num4 != nWidth * nHeight * (m_wbBitmap.Format.BitsPerPixel / 8))
			{
				return null;
			}
			if (isDirect)
			{
				m_wbBitmap.WritePixels(new Int32Rect(0, 0, nWidth, nHeight), intPtr2 + num, num4, m_wbBitmap.PixelWidth * m_wbBitmap.Format.BitsPerPixel / 8);
			}
			else
			{
				result2 = ExternalCallHelper.IntPtr2Bytes(intPtr2 + num, num4);
			}
			return result2;
		}
		catch
		{
			return null;
		}
		finally
		{
			if (intPtr2 != IntPtr.Zero)
			{
				MemoryHelper.UnmapViewOfFile(intPtr2);
			}
			if (intPtr != IntPtr.Zero)
			{
				MemoryHelper.CloseHandle(intPtr);
			}
		}
	}

	private unsafe bool ConvertMono16ToMono8(byte[] bSrcData, int nWidth, int nHeight, byte[] bDstData, bool bIsAuto, int nMinValue = 0, int nMaxValue = 0)
	{
		if (nWidth == 0 || nHeight == 0 || bSrcData == null || bDstData == null)
		{
			return false;
		}
		if (bSrcData.Length != nWidth * nHeight * 2 || bDstData.Length != nWidth * nHeight)
		{
			return false;
		}
		bool flag = bIsAuto;
		int nMax = int.MinValue;
		int nMin = int.MaxValue;
		if (nMinValue >= nMaxValue)
		{
			flag = true;
		}
		fixed (byte* ptr = bSrcData)
		{
			short* bValue = (short*)ptr;
			int length = nHeight * nWidth;
			int toExclusive = (int)Math.Ceiling((double)length / 5000.0);
			if (flag)
			{
				Parallel.For(0, toExclusive, (int i) =>
				{
					int num4 = i * 5000;
					for (int j = 0; j < 5000; j++)
					{
						int num5 = num4 + j;
						if (num5 >= length)
						{
							break;
						}
						short num6 = bValue[num5];
						if (num6 != short.MinValue)
						{
							if (num6 > nMax)
							{
								nMax = num6;
							}
							if (num6 < nMin)
							{
								nMin = num6;
							}
						}
					}
				});
				int num = nMax - nMin;
				if (num <= 0)
				{
					for (int num2 = 0; num2 < length; num2++)
					{
						bDstData[num2] = 0;
					}
				}
				else
				{
					float fCurRange = num;
					Parallel.For(0, toExclusive, (int i) =>
					{
						int num4 = i * 5000;
						for (int j = 0; j < 5000; j++)
						{
							int num5 = num4 + j;
							if (num5 >= length)
							{
								break;
							}
							short num6 = bValue[num5];
							if (num6 == short.MinValue)
							{
								bDstData[num5] = 0;
							}
							else
							{
								float num7 = (float)(num6 - nMin << 8) / fCurRange;
								if (num7 < 0f)
								{
									num7 = 0f;
								}
								else if (num7 > 255f)
								{
									num7 = 255f;
								}
								bDstData[num5] = (byte)num7;
							}
						}
					});
				}
			}
			else
			{
				int num3 = nMaxValue - nMinValue;
				float fCurRange2 = num3;
				Parallel.For(0, toExclusive, (int i) =>
				{
					int num4 = i * 5000;
					for (int j = 0; j < 5000; j++)
					{
						int num5 = num4 + j;
						if (num5 < length)
						{
							short num6 = bValue[num5];
							if (num6 == short.MinValue)
							{
								bDstData[num5] = 0;
							}
							else
							{
								float num7 = (float)(num6 - nMinValue << 8) / fCurRange2;
								if (num7 < 0f)
								{
									num7 = 0f;
								}
								else if (num7 > 255f)
								{
									num7 = 255f;
								}
								bDstData[num5] = (byte)num7;
							}
						}
					}
				});
			}
		}
		return true;
	}
}
