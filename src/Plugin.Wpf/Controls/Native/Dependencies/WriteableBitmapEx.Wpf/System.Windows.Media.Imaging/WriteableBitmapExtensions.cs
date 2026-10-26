using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.IO;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;

public static class WriteableBitmapExtensions
{
	public enum BlendMode
	{
		Alpha,
		Additive,
		Subtractive,
		Mask,
		Multiply,
		ColorKeying,
		None
	}

	private class Edge : IComparable<Edge>
	{
		public readonly int StartX;

		public readonly int StartY;

		public readonly int EndX;

		public readonly int EndY;

		public readonly float Sloap;

		public Edge(int startX, int startY, int endX, int endY)
		{
			if (startY > endY)
			{
				StartX = endX;
				StartY = endY;
				EndX = startX;
				EndY = startY;
			}
			else
			{
				StartX = startX;
				StartY = startY;
				EndX = endX;
				EndY = endY;
			}
			Sloap = (float)(EndX - StartX) / (float)(EndY - StartY);
		}

		public int CompareTo(Edge other)
		{
			if (StartY != other.StartY)
			{
				return StartY.CompareTo(other.StartY);
			}
			return StartX.CompareTo(other.StartX);
		}
	}

	public enum Interpolation
	{
		NearestNeighbor,
		Bilinear
	}

	public enum FlipMode
	{
		Vertical,
		Horizontal
	}

	private static readonly int[] leftEdgeX = new int[8192];

	private static readonly int[] rightEdgeX = new int[8192];

	internal const int SizeOfArgb = 4;

	private const int WhiteR = 255;

	private const int WhiteG = 255;

	private const int WhiteB = 255;

	public static int[,] KernelGaussianBlur5x5 = new int[5, 5]
	{
		{ 1, 4, 7, 4, 1 },
		{ 4, 16, 26, 16, 4 },
		{ 7, 26, 41, 26, 7 },
		{ 4, 16, 26, 16, 4 },
		{ 1, 4, 7, 4, 1 }
	};

	public static int[,] KernelGaussianBlur3x3 = new int[3, 3]
	{
		{ 16, 26, 16 },
		{ 26, 41, 26 },
		{ 16, 26, 16 }
	};

	public static int[,] KernelSharpen3x3 = new int[3, 3]
	{
		{ 0, -2, 0 },
		{ -2, 11, -2 },
		{ 0, -2, 0 }
	};

	private const byte INSIDE = 0;

	private const byte LEFT = 1;

	private const byte RIGHT = 2;

	private const byte BOTTOM = 4;

	private const byte TOP = 8;

	private const float StepFactor = 2f;

	private unsafe static void AAWidthLine(int width, int height, BitmapContext context, float x1, float y1, float x2, float y2, float lineWidth, int color, Rect? clipRect = null)
	{
		if (!CohenSutherlandLineClip(clipRect ?? new Rect(0.0, 0.0, width, height), ref x1, ref y1, ref x2, ref y2) || lineWidth <= 0f)
		{
			return;
		}
		int* pixels = context.Pixels;
		if (y1 > y2)
		{
			Swap(ref x1, ref x2);
			Swap(ref y1, ref y2);
		}
		if (x1 == x2)
		{
			x1 -= (float)((int)lineWidth / 2);
			x2 += (float)((int)lineWidth / 2);
			if (x1 < 0f)
			{
				x1 = 0f;
			}
			if (x2 < 0f || x1 >= (float)width)
			{
				return;
			}
			if (x2 >= (float)width)
			{
				x2 = width - 1;
			}
			if (y1 >= (float)height || y2 < 0f)
			{
				return;
			}
			if (y1 < 0f)
			{
				y1 = 0f;
			}
			if (y2 >= (float)height)
			{
				y2 = height - 1;
			}
			for (int i = (int)x1; (float)i <= x2; i++)
			{
				for (int j = (int)y1; (float)j <= y2; j++)
				{
					byte b = (byte)((color & 0xFF000000u) >> 24);
					byte b2 = (byte)((color & 0xFF0000) >> 16);
					byte b3 = (byte)((color & 0xFF00) >> 8);
					byte b4 = (byte)(color & 0xFF);
					byte b5 = b2;
					byte b6 = b3;
					int num = pixels[j * width + i];
					byte b7 = (byte)((num & 0xFF0000) >> 16);
					byte b8 = (byte)((num & 0xFF00) >> 8);
					byte b9 = (byte)(num & 0xFF);
					b7 = (byte)(b5 * b + b7 * (255 - b) >> 8);
					b8 = (byte)(b6 * b + b8 * (255 - b) >> 8);
					b9 = (byte)(b4 * b + b9 * (255 - b) >> 8);
					pixels[j * width + i] = -16777216 | (b7 << 16) | (b8 << 8) | b9;
				}
			}
			return;
		}
		if (y1 == y2)
		{
			if (x1 > x2)
			{
				Swap(ref x1, ref x2);
			}
			y1 -= (float)((int)lineWidth / 2);
			y2 += (float)((int)lineWidth / 2);
			if (y1 < 0f)
			{
				y1 = 0f;
			}
			if (y2 < 0f || y1 >= (float)height)
			{
				return;
			}
			if (y2 >= (float)height)
			{
				y2 = height - 1;
			}
			if (x1 >= (float)width || y2 < 0f)
			{
				return;
			}
			if (x1 < 0f)
			{
				x1 = 0f;
			}
			if (x2 >= (float)width)
			{
				x2 = width - 1;
			}
			for (int k = (int)x1; (float)k <= x2; k++)
			{
				for (int l = (int)y1; (float)l <= y2; l++)
				{
					byte b10 = (byte)((color & 0xFF000000u) >> 24);
					byte b11 = (byte)((color & 0xFF0000) >> 16);
					byte b12 = (byte)((color & 0xFF00) >> 8);
					byte b13 = (byte)(color & 0xFF);
					byte b14 = b11;
					byte b15 = b12;
					int num2 = pixels[l * width + k];
					byte b16 = (byte)((num2 & 0xFF0000) >> 16);
					byte b17 = (byte)((num2 & 0xFF00) >> 8);
					byte b18 = (byte)(num2 & 0xFF);
					b16 = (byte)(b14 * b10 + b16 * (255 - b10) >> 8);
					b17 = (byte)(b15 * b10 + b17 * (255 - b10) >> 8);
					b18 = (byte)(b13 * b10 + b18 * (255 - b10) >> 8);
					pixels[l * width + k] = -16777216 | (b16 << 16) | (b17 << 8) | b18;
				}
			}
			return;
		}
		y1++;
		y2++;
		_ = (y2 - y1) / (x2 - x1);
		_ = (x2 - x1) / (y2 - y1);
		float num3 = x2 - x1;
		float num4 = y2 - y1;
		float num5 = (float)((double)(lineWidth * num4) / Math.Sqrt(num3 * num3 + num4 * num4));
		float num6 = (float)((double)(lineWidth * num3) / Math.Sqrt(num3 * num3 + num4 * num4));
		_ = num3 * num4 / (num3 * num3 + num4 * num4);
		x1 += num5 / 2f;
		y1 -= num6 / 2f;
		x2 += num5 / 2f;
		y2 -= num6 / 2f;
		float num7 = 0f - num5;
		float num8 = num6;
		int num9 = (int)x1;
		int num10 = (int)y1;
		int num11 = (int)x2;
		int num12 = (int)y2;
		int num13 = (int)(x1 + num7);
		int num14 = (int)(y1 + num8);
		int num15 = (int)(x2 + num7);
		int num16 = (int)(y2 + num8);
		if (num9 == num11)
		{
			num11++;
		}
		if (num13 == num15)
		{
			num15++;
		}
		if (lineWidth == 2f)
		{
			if (Math.Abs(num4) < Math.Abs(num3))
			{
				if (x1 < x2)
				{
					num14 = num10 + 2;
					num16 = num12 + 2;
				}
				else
				{
					num10 = num14 + 2;
					num12 = num16 + 2;
				}
			}
			else
			{
				num9 = num13 + 2;
				num11 = num15 + 2;
			}
		}
		int num17 = Math.Min(Math.Min(num10, num12), Math.Min(num14, num16));
		int num18 = Math.Max(Math.Max(num10, num12), Math.Max(num14, num16));
		if (num17 < 0)
		{
			num17 = -1;
		}
		if (num18 >= height)
		{
			num18 = height + 1;
		}
		for (int m = num17 + 1; m < num18 - 1; m++)
		{
			leftEdgeX[m] = -65536;
			rightEdgeX[m] = 32768;
		}
		AALineQ1(width, height, context, num9, num10, num11, num12, color, num8 > 0f, leftEdge: false);
		AALineQ1(width, height, context, num13, num14, num15, num16, color, num8 < 0f, leftEdge: true);
		if (lineWidth > 1f)
		{
			AALineQ1(width, height, context, num9, num10, num13, num14, color, minEdge: true, num8 > 0f);
			AALineQ1(width, height, context, num11, num12, num15, num16, color, minEdge: false, num8 < 0f);
		}
		if (x1 < x2)
		{
			if (num12 >= 0 && num12 < height)
			{
				rightEdgeX[num12] = Math.Min(num11, rightEdgeX[num12]);
			}
			if (num14 >= 0 && num14 < height)
			{
				leftEdgeX[num14] = Math.Max(num13, leftEdgeX[num14]);
			}
		}
		else
		{
			if (num10 >= 0 && num10 < height)
			{
				rightEdgeX[num10] = Math.Min(num9, rightEdgeX[num10]);
			}
			if (num16 >= 0 && num16 < height)
			{
				leftEdgeX[num16] = Math.Max(num15, leftEdgeX[num16]);
			}
		}
		for (int n = num17 + 1; n < num18 - 1; n++)
		{
			leftEdgeX[n] = Math.Max(leftEdgeX[n], 0);
			rightEdgeX[n] = Math.Min(rightEdgeX[n], width - 1);
			for (int num19 = leftEdgeX[n]; num19 <= rightEdgeX[n]; num19++)
			{
				byte b19 = (byte)((color & 0xFF000000u) >> 24);
				byte b20 = (byte)((color & 0xFF0000) >> 16);
				byte b21 = (byte)((color & 0xFF00) >> 8);
				byte b22 = (byte)(color & 0xFF);
				byte b23 = b20;
				byte b24 = b21;
				int num20 = pixels[n * width + num19];
				byte b25 = (byte)((num20 & 0xFF0000) >> 16);
				byte b26 = (byte)((num20 & 0xFF00) >> 8);
				byte b27 = (byte)(num20 & 0xFF);
				b25 = (byte)(b23 * b19 + b25 * (255 - b19) >> 8);
				b26 = (byte)(b24 * b19 + b26 * (255 - b19) >> 8);
				b27 = (byte)(b22 * b19 + b27 * (255 - b19) >> 8);
				pixels[n * width + num19] = -16777216 | (b25 << 16) | (b26 << 8) | b27;
			}
		}
	}

	private static void Swap<T>(ref T a, ref T b)
	{
		T val = a;
		a = b;
		b = val;
	}

	private unsafe static void AALineQ1(int width, int height, BitmapContext context, int x1, int y1, int x2, int y2, int color, bool minEdge, bool leftEdge)
	{
		byte b = 0;
		if (minEdge)
		{
			b = byte.MaxValue;
		}
		if (x1 == x2 || y1 == y2)
		{
			return;
		}
		int* pixels = context.Pixels;
		if (y1 > y2)
		{
			Swap(ref x1, ref x2);
			Swap(ref y1, ref y2);
		}
		int num = x2 - x1;
		int num2 = y2 - y1;
		if (x1 > x2)
		{
			num = x1 - x2;
		}
		int num3 = x1;
		int num4 = y1;
		ushort num5 = 0;
		num5 = ((num <= num2) ? ((ushort)((num << 16) / num2)) : ((ushort)((num2 << 16) / num)));
		ushort num6 = 0;
		byte b2 = (byte)((color & 0xFF000000u) >> 24);
		byte b3 = (byte)((color & 0xFF0000) >> 16);
		byte b4 = (byte)((color & 0xFF00) >> 8);
		byte b5 = (byte)(color & 0xFF);
		byte b6 = b2;
		num6 = 0;
		if (num >= num2)
		{
			while (num-- != 0)
			{
				if ((ushort)(num6 + num5) <= num6)
				{
					num4++;
				}
				num6 += num5;
				num3 = ((x1 >= x2) ? (num3 - 1) : (num3 + 1));
				if (num4 >= 0 && num4 < height)
				{
					if (leftEdge)
					{
						leftEdgeX[num4] = Math.Max(num3 + 1, leftEdgeX[num4]);
					}
					else
					{
						rightEdgeX[num4] = Math.Min(num3 - 1, rightEdgeX[num4]);
					}
					if (num3 >= 0 && num3 < width)
					{
						b6 = (byte)(b2 * (ushort)((ushort)(num6 >> 8) ^ b) >> 8);
						byte b7 = b3;
						byte b8 = b4;
						byte b9 = b5;
						int num7 = pixels[num4 * width + num3];
						byte b10 = (byte)((num7 & 0xFF0000) >> 16);
						byte b11 = (byte)((num7 & 0xFF00) >> 8);
						byte b12 = (byte)(num7 & 0xFF);
						b10 = (byte)(b7 * b6 + b10 * (255 - b6) >> 8);
						b11 = (byte)(b8 * b6 + b11 * (255 - b6) >> 8);
						b12 = (byte)(b9 * b6 + b12 * (255 - b6) >> 8);
						pixels[num4 * width + num3] = -16777216 | (b10 << 16) | (b11 << 8) | b12;
					}
				}
			}
			return;
		}
		b ^= 0xFF;
		while (--num2 != 0)
		{
			if ((ushort)(num6 + num5) <= num6)
			{
				num3 = ((x1 >= x2) ? (num3 - 1) : (num3 + 1));
			}
			num6 += num5;
			num4++;
			if (num3 >= 0 && num3 < width && num4 >= 0 && num4 < height)
			{
				b6 = (byte)(b2 * (ushort)((ushort)(num6 >> 8) ^ b) >> 8);
				byte b13 = b3;
				byte b8 = b4;
				byte b9 = b5;
				int num8 = pixels[num4 * width + num3];
				byte b10 = (byte)((num8 & 0xFF0000) >> 16);
				byte b11 = (byte)((num8 & 0xFF00) >> 8);
				byte b12 = (byte)(num8 & 0xFF);
				b10 = (byte)(b13 * b6 + b10 * (255 - b6) >> 8);
				b11 = (byte)(b8 * b6 + b11 * (255 - b6) >> 8);
				b12 = (byte)(b9 * b6 + b12 * (255 - b6) >> 8);
				pixels[num4 * width + num3] = -16777216 | (b10 << 16) | (b11 << 8) | b12;
				if (leftEdge)
				{
					leftEdgeX[num4] = num3 + 1;
				}
				else
				{
					rightEdgeX[num4] = num3 - 1;
				}
			}
		}
	}

	public static int ConvertColor(double opacity, Color color)
	{
		if (opacity < 0.0 || opacity > 1.0)
		{
			throw new ArgumentOutOfRangeException("opacity", "Opacity must be between 0.0 and 1.0");
		}
		color.A = (byte)((double)(int)color.A * opacity);
		return ConvertColor(color);
	}

	public static int ConvertColor(Color color)
	{
		int result = 0;
		if (color.A != 0)
		{
			int num = color.A + 1;
			result = (color.A << 24) | ((byte)(color.R * num >> 8) << 16) | ((byte)(color.G * num >> 8) << 8) | (byte)(color.B * num >> 8);
		}
		return result;
	}

	public unsafe static void Clear(this WriteableBitmap bmp, Color color)
	{
		int num = ConvertColor(color);
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int* pixels = bitmapContext.Pixels;
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int num2 = width * 4;
		for (int i = 0; i < width; i++)
		{
			pixels[i] = num;
		}
		int num3 = 1;
		int num4 = 1;
		while (num4 < height)
		{
			BitmapContext.BlockCopy(bitmapContext, 0, bitmapContext, num4 * num2, num3 * num2);
			num4 += num3;
			num3 = Math.Min(2 * num3, height - num4);
		}
	}

	public static void Clear(this WriteableBitmap bmp)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Clear();
	}

	public static WriteableBitmap Clone(this WriteableBitmap bmp)
	{
		using BitmapContext src = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		WriteableBitmap writeableBitmap = BitmapFactory.New(src.Width, src.Height);
		using (BitmapContext dest = writeableBitmap.GetBitmapContext())
		{
			BitmapContext.BlockCopy(src, 0, dest, 0, src.Length * 4);
		}
		return writeableBitmap;
	}

	public unsafe static void ForEach(this WriteableBitmap bmp, Func<int, int, Color> func)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int* pixels = bitmapContext.Pixels;
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int num = 0;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				Color color = func(j, i);
				pixels[num++] = ConvertColor(color);
			}
		}
	}

	public unsafe static void ForEach(this WriteableBitmap bmp, Func<int, int, Color, Color> func)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int* pixels = bitmapContext.Pixels;
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int num = 0;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int num2 = pixels[num];
				int num3;
				int a = (num3 = (byte)(num2 >> 24));
				if (num3 == 0)
				{
					num3 = 1;
				}
				num3 = 65280 / num3;
				Color arg = Color.FromArgb((byte)a, (byte)(((num2 >> 16) & 0xFF) * num3 >> 8), (byte)(((num2 >> 8) & 0xFF) * num3 >> 8), (byte)((num2 & 0xFF) * num3 >> 8));
				Color color = func(j, i, arg);
				pixels[num++] = ConvertColor(color);
			}
		}
	}

	public unsafe static int GetPixeli(this WriteableBitmap bmp, int x, int y)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		return bitmapContext.Pixels[y * bitmapContext.Width + x];
	}

	public unsafe static Color GetPixel(this WriteableBitmap bmp, int x, int y)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int num = bitmapContext.Pixels[y * bitmapContext.Width + x];
		int num2;
		int a = (num2 = (byte)(num >> 24));
		if (num2 == 0)
		{
			num2 = 1;
		}
		num2 = 65280 / num2;
		return Color.FromArgb((byte)a, (byte)(((num >> 16) & 0xFF) * num2 >> 8), (byte)(((num >> 8) & 0xFF) * num2 >> 8), (byte)((num & 0xFF) * num2 >> 8));
	}

	public unsafe static byte GetBrightness(this WriteableBitmap bmp, int x, int y)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int num = bitmapContext.Pixels[y * bitmapContext.Width + x];
		byte b = (byte)(num >> 16);
		byte b2 = (byte)(num >> 8);
		byte b3 = (byte)num;
		return (byte)(b * 6966 + b2 * 23436 + b3 * 2366 >> 15);
	}

	public unsafe static void SetPixeli(this WriteableBitmap bmp, int index, byte r, byte g, byte b)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[index] = -16777216 | (r << 16) | (g << 8) | b;
	}

	public unsafe static void SetPixel(this WriteableBitmap bmp, int x, int y, byte r, byte g, byte b)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[y * bitmapContext.Width + x] = -16777216 | (r << 16) | (g << 8) | b;
	}

	public unsafe static void SetPixeli(this WriteableBitmap bmp, int index, byte a, byte r, byte g, byte b)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[index] = (a << 24) | (r << 16) | (g << 8) | b;
	}

	public unsafe static void SetPixel(this WriteableBitmap bmp, int x, int y, byte a, byte r, byte g, byte b)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[y * bitmapContext.Width + x] = (a << 24) | (r << 16) | (g << 8) | b;
	}

	public unsafe static void SetPixeli(this WriteableBitmap bmp, int index, Color color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[index] = ConvertColor(color);
	}

	public unsafe static void SetPixel(this WriteableBitmap bmp, int x, int y, Color color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[y * bitmapContext.Width + x] = ConvertColor(color);
	}

	public unsafe static void SetPixeli(this WriteableBitmap bmp, int index, byte a, Color color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int num = a + 1;
		bitmapContext.Pixels[index] = (a << 24) | ((byte)(color.R * num >> 8) << 16) | ((byte)(color.G * num >> 8) << 8) | (byte)(color.B * num >> 8);
	}

	public unsafe static void SetPixel(this WriteableBitmap bmp, int x, int y, byte a, Color color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int num = a + 1;
		bitmapContext.Pixels[y * bitmapContext.Width + x] = (a << 24) | ((byte)(color.R * num >> 8) << 16) | ((byte)(color.G * num >> 8) << 8) | (byte)(color.B * num >> 8);
	}

	public unsafe static void SetPixeli(this WriteableBitmap bmp, int index, int color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[index] = color;
	}

	public unsafe static void SetPixel(this WriteableBitmap bmp, int x, int y, int color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		bitmapContext.Pixels[y * bitmapContext.Width + x] = color;
	}

	public static void Blit(this WriteableBitmap bmp, Rect destRect, WriteableBitmap source, Rect sourceRect, BlendMode blendMode)
	{
		bmp.Blit(destRect, source, sourceRect, Colors.White, blendMode);
	}

	public static void Blit(this WriteableBitmap bmp, Rect destRect, WriteableBitmap source, Rect sourceRect)
	{
		bmp.Blit(destRect, source, sourceRect, Colors.White, BlendMode.Alpha);
	}

	public static void Blit(this WriteableBitmap bmp, Point destPosition, WriteableBitmap source, Rect sourceRect, Color color, BlendMode blendMode)
	{
		Rect destRect = new Rect(destPosition, new Size(sourceRect.Width, sourceRect.Height));
		bmp.Blit(destRect, source, sourceRect, color, blendMode);
	}

	internal unsafe static void Blit(this WriteableBitmap bmp, Rect destRect, WriteableBitmap source, Rect sourceRect, Color color, BlendMode blendMode)
	{
		if (color.A == 0)
		{
			return;
		}
		int num = (int)destRect.Width;
		int num2 = (int)destRect.Height;
		using BitmapContext src = source.GetBitmapContext(ReadWriteMode.ReadOnly);
		bool flag = src.Format == PixelFormats.Pbgra32 || src.Format == PixelFormats.Prgba64 || src.Format == PixelFormats.Prgba128Float;
		using BitmapContext dest = bmp.GetBitmapContext();
		int width = src.Width;
		int width2 = dest.Width;
		int height = dest.Height;
		Rect rect = new Rect(0.0, 0.0, width2, height);
		rect.Intersect(destRect);
		if (rect.IsEmpty)
		{
			return;
		}
		int* pixels = src.Pixels;
		int* pixels2 = dest.Pixels;
		int length = src.Length;
		int num3 = -1;
		int num4 = (int)destRect.X;
		int num5 = (int)destRect.Y;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		int a = color.A;
		int r = color.R;
		int g = color.G;
		int b = color.B;
		bool flag2 = color != Colors.White;
		int num10 = (int)sourceRect.Width;
		double num11 = sourceRect.Width / destRect.Width;
		double num12 = sourceRect.Height / destRect.Height;
		int num13 = (int)sourceRect.X;
		int num14 = (int)sourceRect.Y;
		int num15 = -1;
		int num16 = -1;
		double num17 = num14;
		int num18 = num5;
		for (int i = 0; i < num2; i++)
		{
			if (num18 >= 0 && num18 < height)
			{
				double num19 = num13;
				int num20 = num4 + num18 * width2;
				int num21 = num4;
				int num22 = *pixels;
				if (blendMode == BlendMode.None && !flag2)
				{
					num3 = (int)num19 + (int)num17 * width;
					int num23 = ((num21 < 0) ? (-num21) : 0);
					int num24 = num21 + num23;
					int num25 = width - num23;
					int num26 = ((num24 + num25 < width2) ? num25 : (width2 - num24));
					if (num26 > num10)
					{
						num26 = num10;
					}
					if (num26 > num)
					{
						num26 = num;
					}
					BitmapContext.BlockCopy(src, (num3 + num23) * 4, dest, (num20 + num23) * 4, num26 * 4);
				}
				else
				{
					for (int j = 0; j < num; j++)
					{
						if (num21 >= 0 && num21 < width2)
						{
							if ((int)num19 != num15 || (int)num17 != num16)
							{
								num3 = (int)num19 + (int)num17 * width;
								if (num3 >= 0 && num3 < length)
								{
									num22 = pixels[num3];
									num9 = (num22 >> 24) & 0xFF;
									num6 = (num22 >> 16) & 0xFF;
									num7 = (num22 >> 8) & 0xFF;
									num8 = num22 & 0xFF;
									if (flag2 && num9 != 0)
									{
										num9 = num9 * a * 32897 >> 23;
										num6 = (num6 * r * 32897 >> 23) * a * 32897 >> 23;
										num7 = (num7 * g * 32897 >> 23) * a * 32897 >> 23;
										num8 = (num8 * b * 32897 >> 23) * a * 32897 >> 23;
										num22 = (num9 << 24) | (num6 << 16) | (num7 << 8) | num8;
									}
								}
								else
								{
									num9 = 0;
								}
							}
							switch (blendMode)
							{
							case BlendMode.None:
								pixels2[num20] = num22;
								break;
							case BlendMode.ColorKeying:
								num6 = (num22 >> 16) & 0xFF;
								num7 = (num22 >> 8) & 0xFF;
								num8 = num22 & 0xFF;
								if (num6 != color.R || num7 != color.G || num8 != color.B)
								{
									pixels2[num20] = num22;
								}
								break;
							case BlendMode.Mask:
							{
								int num42 = pixels2[num20];
								int num28 = (num42 >> 24) & 0xFF;
								int num29 = (num42 >> 16) & 0xFF;
								int num30 = (num42 >> 8) & 0xFF;
								int num31 = num42 & 0xFF;
								num42 = (num28 * num9 * 32897 >> 23 << 24) | (num29 * num9 * 32897 >> 23 << 16) | (num30 * num9 * 32897 >> 23 << 8) | (num31 * num9 * 32897 >> 23);
								pixels2[num20] = num42;
								break;
							}
							default:
							{
								if (num9 <= 0)
								{
									break;
								}
								int num27 = pixels2[num20];
								int num28 = (num27 >> 24) & 0xFF;
								if ((num9 == 255 || num28 == 0) && blendMode != BlendMode.Additive && blendMode != BlendMode.Subtractive && blendMode != BlendMode.Multiply)
								{
									pixels2[num20] = num22;
									break;
								}
								int num29 = (num27 >> 16) & 0xFF;
								int num30 = (num27 >> 8) & 0xFF;
								int num31 = num27 & 0xFF;
								switch (blendMode)
								{
								case BlendMode.Alpha:
								{
									int num41 = 255 - num9;
									num27 = ((!flag) ? (((num28 & 0xFF) << 24) | (((num6 * num9 + num41 * num29 >> 8) & 0xFF) << 16) | (((num7 * num9 + num41 * num30 >> 8) & 0xFF) << 8) | ((num8 * num9 + num41 * num31 >> 8) & 0xFF)) : (((num28 & 0xFF) << 24) | ((((num6 << 8) + num41 * num29 >> 8) & 0xFF) << 16) | ((((num7 << 8) + num41 * num30 >> 8) & 0xFF) << 8) | (((num8 << 8) + num41 * num31 >> 8) & 0xFF)));
									break;
								}
								case BlendMode.Additive:
								{
									int num40 = ((255 <= num9 + num28) ? 255 : (num9 + num28));
									num27 = (num40 << 24) | (((num40 <= num6 + num29) ? num40 : (num6 + num29)) << 16) | (((num40 <= num7 + num30) ? num40 : (num7 + num30)) << 8) | ((num40 <= num8 + num31) ? num40 : (num8 + num31));
									break;
								}
								case BlendMode.Subtractive:
									num27 = (num28 << 24) | (((num6 < num29) ? (num6 - num29) : 0) << 16) | (((num7 < num30) ? (num7 - num30) : 0) << 8) | ((num8 < num31) ? (num8 - num31) : 0);
									break;
								case BlendMode.Multiply:
								{
									int num32 = num9 * num28 + 128;
									int num33 = num6 * num29 + 128;
									int num34 = num7 * num30 + 128;
									int num35 = num8 * num31 + 128;
									int num36 = (num32 >> 8) + num32 >> 8;
									int num37 = (num33 >> 8) + num33 >> 8;
									int num38 = (num34 >> 8) + num34 >> 8;
									int num39 = (num35 >> 8) + num35 >> 8;
									num27 = (num36 << 24) | (((num36 <= num37) ? num36 : num37) << 16) | (((num36 <= num38) ? num36 : num38) << 8) | ((num36 <= num39) ? num36 : num39);
									break;
								}
								}
								pixels2[num20] = num27;
								break;
							}
							}
						}
						num21++;
						num20++;
						num19 += num11;
					}
				}
			}
			num17 += num12;
			num18++;
		}
	}

	public unsafe static void Blit(BitmapContext destContext, int dpw, int dph, Rect destRect, BitmapContext srcContext, Rect sourceRect, int sourceWidth)
	{
		int num = (int)destRect.Width;
		int num2 = (int)destRect.Height;
		Rect rect = new Rect(0.0, 0.0, dpw, dph);
		rect.Intersect(destRect);
		if (rect.IsEmpty)
		{
			return;
		}
		bool flag = srcContext.Format == PixelFormats.Pbgra32 || srcContext.Format == PixelFormats.Prgba64 || srcContext.Format == PixelFormats.Prgba128Float;
		int* pixels = srcContext.Pixels;
		int* pixels2 = destContext.Pixels;
		int length = srcContext.Length;
		int num3 = -1;
		int num4 = (int)destRect.X;
		int num5 = (int)destRect.Y;
		int num6 = 0;
		int num7 = 0;
		int num8 = 0;
		int num9 = 0;
		_ = sourceRect.Width;
		double num10 = sourceRect.Width / destRect.Width;
		double num11 = sourceRect.Height / destRect.Height;
		int num12 = (int)sourceRect.X;
		int num13 = (int)sourceRect.Y;
		int num14 = -1;
		int num15 = -1;
		double num16 = num13;
		int num17 = num5;
		for (int i = 0; i < num2; i++)
		{
			if (num17 >= 0 && num17 < dph)
			{
				double num18 = num12;
				int num19 = num4 + num17 * dpw;
				int num20 = num4;
				int num21 = *pixels;
				for (int j = 0; j < num; j++)
				{
					if (num20 >= 0 && num20 < dpw)
					{
						if ((int)num18 != num14 || (int)num16 != num15)
						{
							num3 = (int)num18 + (int)num16 * sourceWidth;
							if (num3 >= 0 && num3 < length)
							{
								num21 = pixels[num3];
								num9 = (num21 >> 24) & 0xFF;
								num6 = (num21 >> 16) & 0xFF;
								num7 = (num21 >> 8) & 0xFF;
								num8 = num21 & 0xFF;
							}
							else
							{
								num9 = 0;
							}
						}
						if (num9 > 0)
						{
							int num22 = pixels2[num19];
							int num23 = (num22 >> 24) & 0xFF;
							if (num9 == 255 || num23 == 0)
							{
								pixels2[num19] = num21;
							}
							else
							{
								int num24 = (num22 >> 16) & 0xFF;
								int num25 = (num22 >> 8) & 0xFF;
								int num26 = num22 & 0xFF;
								int num27 = 255 - num9;
								num22 = ((!flag) ? (((num23 & 0xFF) << 24) | (((num6 * num9 + num27 * num24 >> 8) & 0xFF) << 16) | (((num7 * num9 + num27 * num25 >> 8) & 0xFF) << 8) | ((num8 * num9 + num27 * num26 >> 8) & 0xFF)) : (((num23 & 0xFF) << 24) | ((((num6 << 8) + num27 * num24 >> 8) & 0xFF) << 16) | ((((num7 << 8) + num27 * num25 >> 8) & 0xFF) << 8) | (((num8 << 8) + num27 * num26 >> 8) & 0xFF)));
								pixels2[num19] = num22;
							}
						}
					}
					num20++;
					num19++;
					num18 += num10;
				}
			}
			num16 += num11;
			num17++;
		}
	}

	public unsafe static void BlitRender(this WriteableBitmap bmp, WriteableBitmap source, bool shouldClear = true, float opacity = 1f, GeneralTransform transform = null)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		if (transform == null)
		{
			transform = new MatrixTransform();
		}
		int* pixels = bitmapContext.Pixels;
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		GeneralTransform inverse = transform.Inverse;
		if (shouldClear)
		{
			bitmapContext.Clear();
		}
		using BitmapContext bitmapContext2 = source.GetBitmapContext(ReadWriteMode.ReadOnly);
		int* pixels2 = bitmapContext2.Pixels;
		int width2 = bitmapContext2.Width;
		int height2 = bitmapContext2.Height;
		Rect rect = new Rect(0.0, 0.0, width2, height2);
		Rect rect2 = new Rect(0.0, 0.0, width, height);
		Rect rect3 = transform.TransformBounds(rect);
		rect3.Intersect(rect2);
		int num = (int)rect3.Left;
		int num2 = (int)rect3.Top;
		int num3 = (int)rect3.Right;
		int num4 = (int)rect3.Bottom;
		Point point = inverse.Transform(new Point(num, num2));
		Point point2 = inverse.Transform(new Point(num + 1, num2));
		Point point3 = inverse.Transform(new Point(num, num2 + 1));
		float num5 = (float)point.X;
		float num6 = (float)point.Y;
		int num7 = (int)(((float)point2.X - num5) * 1024f);
		int num8 = (int)(((float)point2.Y - num6) * 1024f);
		int num9 = (int)(((float)point3.X - num5) * 1024f);
		int num10 = (int)(((float)point3.Y - num6) * 1024f);
		int num11 = (int)((float)point.X * 1024f);
		int num12 = (int)((float)point.Y * 1024f);
		int num13 = width2 << 10;
		int num14 = height2 << 10;
		int num15 = (int)(opacity * 255f);
		int num16 = 0;
		for (int i = num2; i < num4; i++)
		{
			num16 = i * width + num;
			int num17 = num11;
			int num18 = num12;
			for (int j = num; j < num3; j++)
			{
				if (num11 >= 0 && num11 < num13 && num12 >= 0 && num12 < num14)
				{
					int num19 = num11 >> 10;
					int num20 = num12 >> 10;
					if (num19 < 0)
					{
						num19 = 0;
					}
					if (num20 < 0)
					{
						num20 = 0;
					}
					int num21 = num19 + 1;
					int num22 = num20 + 1;
					if (num21 >= width2)
					{
						num19 = width2 - 1;
						num21 = 0;
					}
					else
					{
						num21 = 1;
					}
					if (num22 >= height2)
					{
						num20 = height2 - 1;
						num22 = 0;
					}
					else
					{
						num22 = width2;
					}
					int num23 = num20 * width2 + num19;
					int num24 = pixels2[num23];
					int num25 = pixels2[num23 + num21];
					int num26 = pixels2[num23 + num22];
					int num27 = pixels2[num23 + num22 + num21];
					int num28 = num11 & 0x3FF;
					int num29 = num12 & 0x3FF;
					byte b = (byte)(num24 >> 24);
					byte b2 = (byte)(num25 >> 24);
					byte b3 = (byte)(num26 >> 24);
					byte b4 = (byte)(num27 >> 24);
					byte b5;
					int num30;
					int num31;
					if (b == b2 && b == b3 && b == b4)
					{
						if (b == 0)
						{
							pixels[num16] = 0;
							num11 += num7;
							num12 += num8;
							num16++;
							continue;
						}
						b5 = b;
					}
					else
					{
						num30 = b + (num28 * (b2 - b) >> 10);
						num31 = b3 + (num28 * (b4 - b3) >> 10);
						b5 = (byte)(num30 + (num29 * (num31 - num30) >> 10));
					}
					num30 = (byte)(num24 >> 16) + (num28 * ((byte)(num25 >> 16) - (byte)(num24 >> 16)) >> 10);
					num31 = (byte)(num26 >> 16) + (num28 * ((byte)(num27 >> 16) - (byte)(num26 >> 16)) >> 10);
					byte b6 = (byte)(num30 + (num29 * (num31 - num30) >> 10));
					num30 = (byte)(num24 >> 8) + (num28 * ((byte)(num25 >> 8) - (byte)(num24 >> 8)) >> 10);
					num31 = (byte)(num26 >> 8) + (num28 * ((byte)(num27 >> 8) - (byte)(num26 >> 8)) >> 10);
					byte b7 = (byte)(num30 + (num29 * (num31 - num30) >> 10));
					num30 = (byte)num24 + (num28 * ((byte)num25 - (byte)num24) >> 10);
					num31 = (byte)num26 + (num28 * ((byte)num27 - (byte)num26) >> 10);
					byte b8 = (byte)(num30 + (num29 * (num31 - num30) >> 10));
					if (num15 != 255)
					{
						b5 = (byte)(b5 * num15 >> 8);
						b6 = (byte)(b6 * num15 >> 8);
						b7 = (byte)(b7 * num15 >> 8);
						b8 = (byte)(b8 * num15 >> 8);
					}
					pixels[num16] = (b5 << 24) | (b6 << 16) | (b7 << 8) | b8;
				}
				num11 += num7;
				num12 += num8;
				num16++;
			}
			num11 = num17 + num9;
			num12 = num18 + num10;
		}
	}

	public static byte[] ToByteArray(this WriteableBitmap bmp, int offset, int count)
	{
		using BitmapContext src = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		if (count == -1)
		{
			count = src.Length;
		}
		int num = count * 4;
		byte[] array = new byte[num];
		BitmapContext.BlockCopy(src, offset, array, 0, num);
		return array;
	}

	public static byte[] ToByteArray(this WriteableBitmap bmp, int count)
	{
		return bmp.ToByteArray(0, count);
	}

	public static byte[] ToByteArray(this WriteableBitmap bmp)
	{
		return bmp.ToByteArray(0, -1);
	}

	public static WriteableBitmap FromByteArray(this WriteableBitmap bmp, byte[] buffer, int offset, int count)
	{
		using BitmapContext dest = bmp.GetBitmapContext();
		BitmapContext.BlockCopy(buffer, offset, dest, 0, count);
		return bmp;
	}

	public static WriteableBitmap FromByteArray(this WriteableBitmap bmp, byte[] buffer, int count)
	{
		return bmp.FromByteArray(buffer, 0, count);
	}

	public static WriteableBitmap FromByteArray(this WriteableBitmap bmp, byte[] buffer)
	{
		return bmp.FromByteArray(buffer, 0, buffer.Length);
	}

	public unsafe static void WriteTga(this WriteableBitmap bmp, Stream destination)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		byte[] array = new byte[bitmapContext.Length * 4];
		int num = 0;
		int num2 = width << 2;
		int num3 = width << 3;
		int num4 = (height - 1) * num2;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int num5 = pixels[num];
				byte b = (byte)(num5 >> 24);
				int num6 = b;
				if (num6 == 0)
				{
					num6 = 1;
				}
				num6 = 65280 / num6;
				array[num4 + 3] = b;
				array[num4 + 2] = (byte)(((num5 >> 16) & 0xFF) * num6 >> 8);
				array[num4 + 1] = (byte)(((num5 >> 8) & 0xFF) * num6 >> 8);
				array[num4] = (byte)((num5 & 0xFF) * num6 >> 8);
				num++;
				num4 += 4;
			}
			num4 -= num3;
		}
		byte[] buffer = new byte[18]
		{
			0,
			0,
			2,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			0,
			(byte)(width & 0xFF),
			(byte)((width & 0xFF00) >> 8),
			(byte)(height & 0xFF),
			(byte)((height & 0xFF00) >> 8),
			32,
			0
		};
		using BinaryWriter binaryWriter = new BinaryWriter(destination);
		binaryWriter.Write(buffer);
		binaryWriter.Write(array);
	}

	[Obsolete("Please use BitmapFactory.FromResource instead of this FromResource method.")]
	public static WriteableBitmap FromResource(this WriteableBitmap bmp, string relativePath)
	{
		return BitmapFactory.FromResource(relativePath);
	}

	[Obsolete("Please use BitmapFactory.FromContent instead of this FromContent method.")]
	public static WriteableBitmap FromContent(this WriteableBitmap bmp, string relativePath)
	{
		return BitmapFactory.FromContent(relativePath);
	}

	[Obsolete("Please use BitmapFactory.FromStream instead of this FromStream method.")]
	public static WriteableBitmap FromStream(this WriteableBitmap bmp, Stream stream)
	{
		return BitmapFactory.FromStream(stream);
	}

	public static void FillRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillRectangle(x1, y1, x2, y2, color2);
	}

	public unsafe static void FillRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, bool doAlphaBlend = false)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int num = (color >> 24) & 0xFF;
		int sr = (color >> 16) & 0xFF;
		int sg = (color >> 8) & 0xFF;
		int sb = color & 0xFF;
		bool flag = !doAlphaBlend || num == 255;
		int* pixels = bitmapContext.Pixels;
		if ((x1 < 0 && x2 < 0) || (y1 < 0 && y2 < 0) || (x1 >= width && x2 >= width) || (y1 >= height && y2 >= height))
		{
			return;
		}
		if (x1 < 0)
		{
			x1 = 0;
		}
		if (y1 < 0)
		{
			y1 = 0;
		}
		if (x2 < 0)
		{
			x2 = 0;
		}
		if (y2 < 0)
		{
			y2 = 0;
		}
		if (x1 > width)
		{
			x1 = width;
		}
		if (y1 > height)
		{
			y1 = height;
		}
		if (x2 > width)
		{
			x2 = width;
		}
		if (y2 > height)
		{
			y2 = height;
		}
		if (y1 > y2)
		{
			y2 -= y1;
			y1 += y2;
			y2 = y1 - y2;
		}
		int num2 = y1 * width;
		int num3 = num2 + x1;
		int num4 = num2 + x2;
		for (int i = num3; i < num4; i++)
		{
			pixels[i] = (flag ? color : AlphaBlendColors(pixels[i], num, sr, sg, sb));
		}
		int num5 = x2 - x1;
		int srcOffset = num3 * 4;
		int num6 = y2 * width + x1;
		for (int j = num3 + width; j < num6; j += width)
		{
			if (flag)
			{
				BitmapContext.BlockCopy(bitmapContext, srcOffset, bitmapContext, j * 4, num5 * 4);
				continue;
			}
			for (int k = 0; k < num5; k++)
			{
				int num7 = j + k;
				pixels[num7] = AlphaBlendColors(pixels[num7], num, sr, sg, sb);
			}
		}
	}

	private static int AlphaBlendColors(int pixel, int sa, int sr, int sg, int sb)
	{
		int num = (pixel >> 24) & 0xFF;
		int num2 = (pixel >> 16) & 0xFF;
		int num3 = (pixel >> 8) & 0xFF;
		int num4 = pixel & 0xFF;
		return (sa + (num * (255 - sa) * 32897 >> 23) << 24) | (sr + (num2 * (255 - sa) * 32897 >> 23) << 16) | (sg + (num3 * (255 - sa) * 32897 >> 23) << 8) | (sb + (num4 * (255 - sa) * 32897 >> 23));
	}

	public static void FillEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillEllipse(x1, y1, x2, y2, color2);
	}

	public static void FillEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color)
	{
		int num = x2 - x1 >> 1;
		int num2 = y2 - y1 >> 1;
		int xc = x1 + num;
		int yc = y1 + num2;
		bmp.FillEllipseCentered(xc, yc, num, num2, color);
	}

	public static void FillEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillEllipseCentered(xc, yc, xr, yr, color2);
	}

	public unsafe static void FillEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, int color, bool doAlphaBlend = false)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int* pixels = bitmapContext.Pixels;
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		if (xr < 1 || yr < 1 || xc - xr >= width || xc + xr < 0 || yc - yr >= height || yc + yr < 0)
		{
			return;
		}
		int num = xr;
		int num2 = 0;
		int num3 = xr * xr << 1;
		int num4 = yr * yr << 1;
		int num5 = yr * yr * (1 - (xr << 1));
		int num6 = xr * xr;
		int num7 = 0;
		int num8 = num4 * xr;
		int num9 = 0;
		int num10 = (color >> 24) & 0xFF;
		int sr = (color >> 16) & 0xFF;
		int sg = (color >> 8) & 0xFF;
		int sb = color & 0xFF;
		bool flag = !doAlphaBlend || num10 == 255;
		int num13;
		int num14;
		int num11;
		int num12;
		while (num8 >= num9)
		{
			num11 = yc + num2;
			num12 = yc - num2 - 1;
			if (num11 < 0)
			{
				num11 = 0;
			}
			if (num11 >= height)
			{
				num11 = height - 1;
			}
			if (num12 < 0)
			{
				num12 = 0;
			}
			if (num12 >= height)
			{
				num12 = height - 1;
			}
			num13 = num11 * width;
			num14 = num12 * width;
			int num15 = xc + num;
			int num16 = xc - num;
			if (num15 < 0)
			{
				num15 = 0;
			}
			if (num15 >= width)
			{
				num15 = width - 1;
			}
			if (num16 < 0)
			{
				num16 = 0;
			}
			if (num16 >= width)
			{
				num16 = width - 1;
			}
			if (flag)
			{
				for (int i = num16; i <= num15; i++)
				{
					pixels[i + num13] = color;
					pixels[i + num14] = color;
				}
			}
			else
			{
				for (int j = num16; j <= num15; j++)
				{
					pixels[j + num13] = AlphaBlendColors(pixels[j + num13], num10, sr, sg, sb);
					pixels[j + num14] = AlphaBlendColors(pixels[j + num14], num10, sr, sg, sb);
				}
			}
			num2++;
			num9 += num3;
			num7 += num6;
			num6 += num3;
			if (num5 + (num7 << 1) > 0)
			{
				num--;
				num8 -= num4;
				num7 += num5;
				num5 += num4;
			}
		}
		num = 0;
		num2 = yr;
		num11 = yc + num2;
		num12 = yc - num2;
		if (num11 < 0)
		{
			num11 = 0;
		}
		if (num11 >= height)
		{
			num11 = height - 1;
		}
		if (num12 < 0)
		{
			num12 = 0;
		}
		if (num12 >= height)
		{
			num12 = height - 1;
		}
		num13 = num11 * width;
		num14 = num12 * width;
		num5 = yr * yr;
		num6 = xr * xr * (1 - (yr << 1));
		num7 = 0;
		num8 = 0;
		num9 = num3 * yr;
		while (num8 <= num9)
		{
			int num15 = xc + num;
			int num16 = xc - num;
			if (num15 < 0)
			{
				num15 = 0;
			}
			if (num15 >= width)
			{
				num15 = width - 1;
			}
			if (num16 < 0)
			{
				num16 = 0;
			}
			if (num16 >= width)
			{
				num16 = width - 1;
			}
			if (flag)
			{
				for (int k = num16; k <= num15; k++)
				{
					pixels[k + num13] = color;
					pixels[k + num14] = color;
				}
			}
			else
			{
				for (int l = num16; l <= num15; l++)
				{
					pixels[l + num13] = AlphaBlendColors(pixels[l + num13], num10, sr, sg, sb);
					pixels[l + num14] = AlphaBlendColors(pixels[l + num14], num10, sr, sg, sb);
				}
			}
			num++;
			num8 += num4;
			num7 += num5;
			num5 += num4;
			if (num6 + (num7 << 1) > 0)
			{
				num2--;
				num11 = yc + num2;
				num12 = yc - num2;
				if (num11 < 0)
				{
					num11 = 0;
				}
				if (num11 >= height)
				{
					num11 = height - 1;
				}
				if (num12 < 0)
				{
					num12 = 0;
				}
				if (num12 >= height)
				{
					num12 = height - 1;
				}
				num13 = num11 * width;
				num14 = num12 * width;
				num9 -= num3;
				num7 += num6;
				num6 += num3;
			}
		}
	}

	public static void FillPolygon(this WriteableBitmap bmp, int[] points, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillPolygon(points, color2);
	}

	public unsafe static void FillPolygon(this WriteableBitmap bmp, int[] points, int color, bool doAlphaBlend = false)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int num = (color >> 24) & 0xFF;
		int sr = (color >> 16) & 0xFF;
		int sg = (color >> 8) & 0xFF;
		int sb = color & 0xFF;
		bool flag = !doAlphaBlend || num == 255;
		int* pixels = bitmapContext.Pixels;
		int num2 = points.Length;
		int[] array = new int[points.Length >> 1];
		int num3 = height;
		int num4 = 0;
		for (int i = 1; i < num2; i += 2)
		{
			int num5 = points[i];
			if (num5 < num3)
			{
				num3 = num5;
			}
			if (num5 > num4)
			{
				num4 = num5;
			}
		}
		if (num3 < 0)
		{
			num3 = 0;
		}
		if (num4 >= height)
		{
			num4 = height - 1;
		}
		for (int j = num3; j <= num4; j++)
		{
			float num6 = points[0];
			float num7 = points[1];
			int num8 = 0;
			for (int k = 2; k < num2; k += 2)
			{
				float num9 = points[k];
				float num10 = points[k + 1];
				if ((num7 < (float)j && num10 >= (float)j) || (num10 < (float)j && num7 >= (float)j))
				{
					array[num8++] = (int)(num6 + ((float)j - num7) / (num10 - num7) * (num9 - num6));
				}
				num6 = num9;
				num7 = num10;
			}
			for (int l = 1; l < num8; l++)
			{
				int num11 = array[l];
				int num12 = l;
				while (num12 > 0 && array[num12 - 1] > num11)
				{
					array[num12] = array[num12 - 1];
					num12--;
				}
				array[num12] = num11;
			}
			for (int m = 0; m < num8 - 1; m += 2)
			{
				int num13 = array[m];
				int num14 = array[m + 1];
				if (num14 > 0 && num13 < width)
				{
					if (num13 < 0)
					{
						num13 = 0;
					}
					if (num14 >= width)
					{
						num14 = width - 1;
					}
					for (int n = num13; n <= num14; n++)
					{
						int num15 = j * width + n;
						pixels[num15] = (flag ? color : AlphaBlendColors(pixels[num15], num, sr, sg, sb));
					}
				}
			}
		}
	}

	public static void FillPolygonsEvenOdd(this WriteableBitmap bmp, int[][] polygons, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillPolygonsEvenOdd(polygons, color2);
	}

	public unsafe static void FillPolygonsEvenOdd(this WriteableBitmap bmp, int[][] polygons, int color)
	{
		if (polygons.Length == 0)
		{
			return;
		}
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		List<Edge> list = new List<Edge>();
		int num = 0;
		foreach (int[] array in polygons)
		{
			int num2 = array.Length;
			if (num2 < 6)
			{
				continue;
			}
			int num3;
			int startX;
			int num4;
			if (array[0] != array[num2 - 2] || array[1] != array[num2 - 1])
			{
				num3 = 0;
				startX = array[num2 - 2];
				num4 = array[num2 - 1];
			}
			else
			{
				num3 = 2;
				startX = array[0];
				num4 = array[1];
			}
			for (int j = num3; j < num2; j += 2)
			{
				int num5 = array[j];
				int num6 = array[j + 1];
				if (num6 != num4)
				{
					Edge edge = new Edge(startX, num4, num5, num6);
					if (edge.StartY < height && edge.EndY >= 0)
					{
						if (edge.EndY > num)
						{
							num = edge.EndY;
						}
						list.Add(edge);
					}
				}
				startX = num5;
				num4 = num6;
			}
		}
		if (list.Count == 0)
		{
			return;
		}
		if (num >= height)
		{
			num = height - 1;
		}
		list.Sort();
		int num7 = list[0].StartY;
		if (num7 < 0)
		{
			num7 = 0;
		}
		int[] array2 = new int[list.Count];
		LinkedList<Edge> linkedList = new LinkedList<Edge>();
		int k = 0;
		for (int l = num7; l <= num; l++)
		{
			LinkedListNode<Edge> linkedListNode = linkedList.First;
			while (linkedListNode != null)
			{
				LinkedListNode<Edge> next = linkedListNode.Next;
				if (linkedListNode.Value.EndY <= l)
				{
					linkedList.Remove(linkedListNode);
				}
				linkedListNode = next;
			}
			for (; k < list.Count && list[k].StartY <= l; k++)
			{
				linkedList.AddLast(list[k]);
			}
			int num8 = 0;
			foreach (Edge item in linkedList)
			{
				array2[num8++] = (int)((float)item.StartX + (float)(l - item.StartY) * item.Sloap);
			}
			for (int m = 1; m < num8; m++)
			{
				int num9 = array2[m];
				int num10 = m;
				while (num10 > 0 && array2[num10 - 1] > num9)
				{
					array2[num10] = array2[num10 - 1];
					num10--;
				}
				array2[num10] = num9;
			}
			for (int n = 0; n < num8 - 1; n += 2)
			{
				int num11 = array2[n];
				int num12 = array2[n + 1];
				if (num11 < 0)
				{
					num11 = 0;
				}
				if (num12 >= width)
				{
					num12 = width - 1;
				}
				if (num12 >= num11)
				{
					int num13 = l * width + num11;
					for (int num14 = num11; num14 <= num12; num14++)
					{
						pixels[num13++] = color;
					}
				}
			}
		}
	}

	public static void FillQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillQuad(x1, y1, x2, y2, x3, y3, x4, y4, color2);
	}

	public static void FillQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, int color)
	{
		bmp.FillPolygon(new int[10] { x1, y1, x2, y2, x3, y3, x4, y4, x1, y1 }, color);
	}

	public static void FillTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillTriangle(x1, y1, x2, y2, x3, y3, color2);
	}

	public static void FillTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int color)
	{
		bmp.FillPolygon(new int[8] { x1, y1, x2, y2, x3, y3, x1, y1 }, color);
	}

	[Obsolete("Obsolete, left for compatibility reasons. Please use List<int> ComputeBezierPoints(int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2) instead.")]
	private static List<int> ComputeBezierPoints(int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2, int color, BitmapContext context, int w, int h)
	{
		return ComputeBezierPoints(x1, y1, cx1, cy1, cx2, cy2, x2, y1);
	}

	private static List<int> ComputeBezierPoints(int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2)
	{
		int num = Math.Min(x1, Math.Min(cx1, Math.Min(cx2, x2)));
		int num2 = Math.Min(y1, Math.Min(cy1, Math.Min(cy2, y2)));
		int num3 = Math.Max(x1, Math.Max(cx1, Math.Max(cx2, x2)));
		int num4 = Math.Max(y1, Math.Max(cy1, Math.Max(cy2, y2)));
		int num5 = num3 - num;
		int num6 = num4 - num2;
		if (num5 > num6)
		{
			num6 = num5;
		}
		List<int> list = new List<int>();
		if (num6 != 0)
		{
			float num7 = 2f / (float)num6;
			int num8 = x1;
			int num9 = y1;
			for (float num10 = 0f; num10 <= 1f; num10 += num7)
			{
				float num11 = num10 * num10;
				float num12 = 1f - num10;
				float num13 = num12 * num12;
				num8 = (int)(num12 * num13 * (float)x1 + 3f * num10 * num13 * (float)cx1 + 3f * num12 * num11 * (float)cx2 + num10 * num11 * (float)x2);
				num9 = (int)(num12 * num13 * (float)y1 + 3f * num10 * num13 * (float)cy1 + 3f * num12 * num11 * (float)cy2 + num10 * num11 * (float)y2);
				list.Add(num8);
				list.Add(num9);
			}
			list.Add(x2);
			list.Add(y2);
		}
		return list;
	}

	public static void FillBeziers(this WriteableBitmap bmp, int[] points, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillBeziers(points, color2);
	}

	public static void FillBeziers(this WriteableBitmap bmp, int[] points, int color)
	{
		int x = points[0];
		int y = points[1];
		List<int> list = new List<int>();
		for (int i = 2; i + 5 < points.Length; i += 6)
		{
			int num = points[i + 4];
			int num2 = points[i + 5];
			list.AddRange(ComputeBezierPoints(x, y, points[i], points[i + 1], points[i + 2], points[i + 3], num, num2));
			x = num;
			y = num2;
		}
		bmp.FillPolygon(list.ToArray(), color);
	}

	[Obsolete("Obsolete, left for compatibility reasons. Please use List<int> ComputeSegmentPoints(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension) instead.")]
	private static List<int> ComputeSegmentPoints(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension, int color, BitmapContext context, int w, int h)
	{
		return ComputeSegmentPoints(x1, y1, x2, y2, x3, y3, x4, y4, tension);
	}

	private static List<int> ComputeSegmentPoints(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension)
	{
		int num = Math.Min(x1, Math.Min(x2, Math.Min(x3, x4)));
		int num2 = Math.Min(y1, Math.Min(y2, Math.Min(y3, y4)));
		int num3 = Math.Max(x1, Math.Max(x2, Math.Max(x3, x4)));
		int num4 = Math.Max(y1, Math.Max(y2, Math.Max(y3, y4)));
		int num5 = num3 - num;
		int num6 = num4 - num2;
		if (num5 > num6)
		{
			num6 = num5;
		}
		List<int> list = new List<int>();
		if (num6 != 0)
		{
			float num7 = 2f / (float)num6;
			float num8 = tension * (float)(x3 - x1);
			float num9 = tension * (float)(y3 - y1);
			float num10 = tension * (float)(x4 - x2);
			float num11 = tension * (float)(y4 - y2);
			float num12 = num8 + num10 + (float)(2 * x2) - (float)(2 * x3);
			float num13 = num9 + num11 + (float)(2 * y2) - (float)(2 * y3);
			float num14 = -2f * num8 - num10 - (float)(3 * x2) + (float)(3 * x3);
			float num15 = -2f * num9 - num11 - (float)(3 * y2) + (float)(3 * y3);
			for (float num16 = 0f; num16 <= 1f; num16 += num7)
			{
				float num17 = num16 * num16;
				int item = (int)(num12 * num17 * num16 + num14 * num17 + num8 * num16 + (float)x2);
				int item2 = (int)(num13 * num17 * num16 + num15 * num17 + num9 * num16 + (float)y2);
				list.Add(item);
				list.Add(item2);
			}
			list.Add(x3);
			list.Add(y3);
		}
		return list;
	}

	public static void FillCurve(this WriteableBitmap bmp, int[] points, float tension, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillCurve(points, tension, color2);
	}

	public static void FillCurve(this WriteableBitmap bmp, int[] points, float tension, int color)
	{
		List<int> list = ComputeSegmentPoints(points[0], points[1], points[0], points[1], points[2], points[3], points[4], points[5], tension);
		int i;
		for (i = 2; i < points.Length - 4; i += 2)
		{
			list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension));
		}
		list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 2], points[i + 3], tension));
		bmp.FillPolygon(list.ToArray(), color);
	}

	public static void FillCurveClosed(this WriteableBitmap bmp, int[] points, float tension, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.FillCurveClosed(points, tension, color2);
	}

	public static void FillCurveClosed(this WriteableBitmap bmp, int[] points, float tension, int color)
	{
		int num = points.Length;
		List<int> list = ComputeSegmentPoints(points[num - 2], points[num - 1], points[0], points[1], points[2], points[3], points[4], points[5], tension);
		int i;
		for (i = 2; i < num - 4; i += 2)
		{
			list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension));
		}
		list.AddRange(ComputeSegmentPoints(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[0], points[1], tension));
		list.AddRange(ComputeSegmentPoints(points[i], points[i + 1], points[i + 2], points[i + 3], points[0], points[1], points[2], points[3], tension));
		bmp.FillPolygon(list.ToArray(), color);
	}

	public static WriteableBitmap Convolute(this WriteableBitmap bmp, int[,] kernel)
	{
		int num = 0;
		foreach (int num2 in kernel)
		{
			num += num2;
		}
		return bmp.Convolute(kernel, num, 0);
	}

	public unsafe static WriteableBitmap Convolute(this WriteableBitmap bmp, int[,] kernel, int kernelFactorSum, int kernelOffsetSum)
	{
		int num = kernel.GetUpperBound(0) + 1;
		int num2 = kernel.GetUpperBound(1) + 1;
		if ((num2 & 1) == 0)
		{
			throw new InvalidOperationException("Kernel width must be odd!");
		}
		if ((num & 1) == 0)
		{
			throw new InvalidOperationException("Kernel height must be odd!");
		}
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext();
		int* pixels = bitmapContext.Pixels;
		int* pixels2 = bitmapContext2.Pixels;
		int num3 = 0;
		int num4 = num2 >> 1;
		int num5 = num >> 1;
		for (int i = 0; i < height; i++)
		{
			for (int j = 0; j < width; j++)
			{
				int num6 = 0;
				int num7 = 0;
				int num8 = 0;
				int num9 = 0;
				for (int k = -num4; k <= num4; k++)
				{
					int num10 = k + j;
					if (num10 < 0)
					{
						num10 = 0;
					}
					else if (num10 >= width)
					{
						num10 = width - 1;
					}
					for (int l = -num5; l <= num5; l++)
					{
						int num11 = l + i;
						if (num11 < 0)
						{
							num11 = 0;
						}
						else if (num11 >= height)
						{
							num11 = height - 1;
						}
						int num12 = pixels[num11 * width + num10];
						int num13 = kernel[l + num4, k + num5];
						num6 += ((num12 >> 24) & 0xFF) * num13;
						num7 += ((num12 >> 16) & 0xFF) * num13;
						num8 += ((num12 >> 8) & 0xFF) * num13;
						num9 += (num12 & 0xFF) * num13;
					}
				}
				int num14 = num6 / kernelFactorSum + kernelOffsetSum;
				int num15 = num7 / kernelFactorSum + kernelOffsetSum;
				int num16 = num8 / kernelFactorSum + kernelOffsetSum;
				int num17 = num9 / kernelFactorSum + kernelOffsetSum;
				byte b = (byte)((num14 > 255) ? 255u : ((num14 >= 0) ? ((uint)num14) : 0u));
				byte b2 = (byte)((num15 > 255) ? 255u : ((num15 >= 0) ? ((uint)num15) : 0u));
				byte b3 = (byte)((num16 > 255) ? 255u : ((num16 >= 0) ? ((uint)num16) : 0u));
				byte b4 = (byte)((num17 > 255) ? 255u : ((num17 >= 0) ? ((uint)num17) : 0u));
				pixels2[num3++] = (b << 24) | (b2 << 16) | (b3 << 8) | b4;
			}
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap Invert(this WriteableBitmap bmp)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		WriteableBitmap writeableBitmap = BitmapFactory.New(bitmapContext.Width, bitmapContext.Height);
		using BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext();
		int* pixels = bitmapContext2.Pixels;
		int* pixels2 = bitmapContext.Pixels;
		int length = bitmapContext.Length;
		for (int i = 0; i < length; i++)
		{
			int num = pixels2[i];
			int num2 = (num >> 24) & 0xFF;
			int num3 = (num >> 16) & 0xFF;
			int num4 = (num >> 8) & 0xFF;
			int num5 = num & 0xFF;
			num3 = 255 - num3;
			num4 = 255 - num4;
			num5 = 255 - num5;
			pixels[i] = (num2 << 24) | (num3 << 16) | (num4 << 8) | num5;
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap Gray(this WriteableBitmap bmp)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using (BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext())
		{
			int* pixels2 = bitmapContext2.Pixels;
			int length = bitmapContext.Length;
			for (int i = 0; i < length; i++)
			{
				int num = pixels[i];
				int num2 = (num >> 24) & 0xFF;
				int num3 = (num >> 16) & 0xFF;
				int num4 = (num >> 8) & 0xFF;
				int num5 = num & 0xFF;
				num3 = (num4 = (num5 = num3 * 6966 + num4 * 23436 + num5 * 2366 >> 15));
				pixels2[i] = (num2 << 24) | (num3 << 16) | (num4 << 8) | num5;
			}
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap AdjustContrast(this WriteableBitmap bmp, double level)
	{
		int num = (int)(259.0 * (level + 255.0) / (255.0 * (259.0 - level)) * 255.0);
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using (BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext())
		{
			int* pixels2 = bitmapContext2.Pixels;
			int length = bitmapContext.Length;
			for (int i = 0; i < length; i++)
			{
				int num2 = pixels[i];
				int num3 = (num2 >> 24) & 0xFF;
				int num4 = (num2 >> 16) & 0xFF;
				int num5 = (num2 >> 8) & 0xFF;
				int num6 = num2 & 0xFF;
				num4 = (num * (num4 - 128) >> 8) + 128;
				num5 = (num * (num5 - 128) >> 8) + 128;
				num6 = (num * (num6 - 128) >> 8) + 128;
				num4 = ((num4 >= 0) ? ((num4 > 255) ? 255 : num4) : 0);
				num5 = ((num5 >= 0) ? ((num5 > 255) ? 255 : num5) : 0);
				num6 = ((num6 >= 0) ? ((num6 > 255) ? 255 : num6) : 0);
				pixels2[i] = (num3 << 24) | (num4 << 16) | (num5 << 8) | num6;
			}
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap AdjustBrightness(this WriteableBitmap bmp, int nLevel)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using (BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext())
		{
			int* pixels2 = bitmapContext2.Pixels;
			int length = bitmapContext.Length;
			for (int i = 0; i < length; i++)
			{
				int num = pixels[i];
				int num2 = (num >> 24) & 0xFF;
				int num3 = (num >> 16) & 0xFF;
				int num4 = (num >> 8) & 0xFF;
				int num5 = num & 0xFF;
				num3 += nLevel;
				num4 += nLevel;
				num5 += nLevel;
				num3 = ((num3 >= 0) ? ((num3 > 255) ? 255 : num3) : 0);
				num4 = ((num4 >= 0) ? ((num4 > 255) ? 255 : num4) : 0);
				num5 = ((num5 >= 0) ? ((num5 > 255) ? 255 : num5) : 0);
				pixels2[i] = (num2 << 24) | (num3 << 16) | (num4 << 8) | num5;
			}
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap AdjustGamma(this WriteableBitmap bmp, double value)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using (BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext())
		{
			int* pixels2 = bitmapContext2.Pixels;
			double y = 1.0 / value;
			int length = bitmapContext.Length;
			for (int i = 0; i < length; i++)
			{
				int num = pixels[i];
				int num2 = (num >> 24) & 0xFF;
				int num3 = (num >> 16) & 0xFF;
				int num4 = (num >> 8) & 0xFF;
				int num5 = num & 0xFF;
				num3 = (int)(255.0 * Math.Pow((double)num3 / 255.0, y));
				num4 = (int)(255.0 * Math.Pow((double)num4 / 255.0, y));
				num5 = (int)(255.0 * Math.Pow((double)num5 / 255.0, y));
				num3 = ((num3 >= 0) ? ((num3 > 255) ? 255 : num3) : 0);
				num4 = ((num4 >= 0) ? ((num4 > 255) ? 255 : num4) : 0);
				num5 = ((num5 >= 0) ? ((num5 > 255) ? 255 : num5) : 0);
				pixels2[i] = (num2 << 24) | (num3 << 16) | (num4 << 8) | num5;
			}
		}
		return writeableBitmap;
	}

	public static void DrawLineBresenham(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
	{
		int color2 = ConvertColor(color);
		bmp.DrawLineBresenham(x1, y1, x2, y2, color2, clipRect);
	}

	public unsafe static void DrawLineBresenham(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		int num = 0;
		int num2 = width;
		int num3 = 0;
		int num4 = height;
		if (clipRect.HasValue)
		{
			Rect value = clipRect.Value;
			num = (int)value.X;
			num2 = (int)(value.X + value.Width);
			num3 = (int)value.Y;
			num4 = (int)(value.Y + value.Height);
		}
		int num5 = x2 - x1;
		int num6 = y2 - y1;
		int num7 = 0;
		if (num5 < 0)
		{
			num5 = -num5;
			num7 = -1;
		}
		else if (num5 > 0)
		{
			num7 = 1;
		}
		int num8 = 0;
		if (num6 < 0)
		{
			num6 = -num6;
			num8 = -1;
		}
		else if (num6 > 0)
		{
			num8 = 1;
		}
		int num9;
		int num10;
		int num11;
		int num12;
		int num13;
		int num14;
		if (num5 > num6)
		{
			num9 = num7;
			num10 = 0;
			num11 = num7;
			num12 = num8;
			num13 = num6;
			num14 = num5;
		}
		else
		{
			num9 = 0;
			num10 = num8;
			num11 = num7;
			num12 = num8;
			num13 = num5;
			num14 = num6;
		}
		int num15 = x1;
		int num16 = y1;
		int num17 = num14 >> 1;
		if (num16 < num4 && num16 >= num3 && num15 < num2 && num15 >= num)
		{
			pixels[num16 * width + num15] = color;
		}
		for (int i = 0; i < num14; i++)
		{
			num17 -= num13;
			if (num17 < 0)
			{
				num17 += num14;
				num15 += num11;
				num16 += num12;
			}
			else
			{
				num15 += num9;
				num16 += num10;
			}
			if (num16 < num4 && num16 >= num3 && num15 < num2 && num15 >= num)
			{
				pixels[num16 * width + num15] = color;
			}
		}
	}

	public static void DrawLineDDA(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
	{
		int color2 = ConvertColor(color);
		bmp.DrawLineDDA(x1, y1, x2, y2, color2, clipRect);
	}

	public unsafe static void DrawLineDDA(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		int num = 0;
		int num2 = width;
		int num3 = 0;
		int num4 = height;
		if (clipRect.HasValue)
		{
			Rect value = clipRect.Value;
			num = (int)value.X;
			num2 = (int)(value.X + value.Width);
			num3 = (int)value.Y;
			num4 = (int)(value.Y + value.Height);
		}
		int num5 = x2 - x1;
		int num6 = y2 - y1;
		int num7 = ((num6 >= 0) ? num6 : (-num6));
		int num8 = ((num5 >= 0) ? num5 : (-num5));
		if (num8 > num7)
		{
			num7 = num8;
		}
		if (num7 == 0)
		{
			return;
		}
		float num9 = (float)num5 / (float)num7;
		float num10 = (float)num6 / (float)num7;
		float num11 = x1;
		float num12 = y1;
		for (int i = 0; i < num7; i++)
		{
			if (num12 < (float)num4 && num12 >= (float)num3 && num11 < (float)num2 && num11 >= (float)num)
			{
				pixels[(int)num12 * width + (int)num11] = color;
			}
			num11 += num9;
			num12 += num10;
		}
	}

	public static void DrawLine(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
	{
		int color2 = ConvertColor(color);
		bmp.DrawLine(x1, y1, x2, y2, color2, clipRect);
	}

	public static void DrawLine(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		DrawLine(context, context.Width, context.Height, x1, y1, x2, y2, color, clipRect);
	}

	public unsafe static void DrawLine(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
	{
		int num = 0;
		int num2 = pixelWidth;
		int num3 = 0;
		int num4 = pixelHeight;
		if (clipRect.HasValue)
		{
			Rect value = clipRect.Value;
			num = (int)value.X;
			num2 = (int)(value.X + value.Width);
			num3 = (int)value.Y;
			num4 = (int)(value.Y + value.Height);
		}
		if (!CohenSutherlandLineClip(new Rect(num, num3, num2 - num, num4 - num3), ref x1, ref y1, ref x2, ref y2))
		{
			return;
		}
		int* pixels = context.Pixels;
		int num5 = x2 - x1;
		int num6 = y2 - y1;
		int num7 = ((num6 < 0) ? (-num6) : num6);
		int num8 = ((num5 < 0) ? (-num5) : num5);
		if (num8 > num7)
		{
			if (num5 < 0)
			{
				int num9 = x1;
				x1 = x2;
				x2 = num9;
				int num10 = y1;
				y1 = y2;
				y2 = num10;
			}
			int num11 = (num6 << 8) / num5;
			int num12 = y1 << 8;
			int num13 = y2 << 8;
			int num14 = pixelHeight << 8;
			if (y1 < y2)
			{
				if (y1 >= num4 || y2 < num3)
				{
					return;
				}
				if (num12 < 0)
				{
					if (num11 == 0)
					{
						return;
					}
					int num15 = num12;
					num12 = num11 - 1 + (num12 + 1) % num11;
					x1 += (num12 - num15) / num11;
				}
				if (num13 >= num14 && num11 != 0)
				{
					num13 = num14 - 1 - (num14 - 1 - num12) % num11;
					x2 = x1 + (num13 - num12) / num11;
				}
			}
			else
			{
				if (y2 >= num4 || y1 < num3)
				{
					return;
				}
				if (num12 >= num14)
				{
					if (num11 == 0)
					{
						return;
					}
					int num16 = num12;
					num12 = num14 - 1 + (num11 - (num14 - 1 - num16) % num11);
					x1 += (num12 - num16) / num11;
				}
				if (num13 < 0 && num11 != 0)
				{
					num13 = num12 % num11;
					x2 = x1 + (num13 - num12) / num11;
				}
			}
			if (x1 < 0)
			{
				num12 -= num11 * x1;
				x1 = 0;
			}
			if (x2 >= pixelWidth)
			{
				x2 = pixelWidth - 1;
			}
			int num17 = num12;
			int num18 = num17 >> 8;
			int num19 = num18;
			int num20 = x1 + num18 * pixelWidth;
			int num21 = ((num11 < 0) ? (1 - pixelWidth) : (1 + pixelWidth));
			for (int i = x1; i <= x2; i++)
			{
				pixels[num20] = color;
				num17 += num11;
				num18 = num17 >> 8;
				if (num18 != num19)
				{
					num19 = num18;
					num20 += num21;
				}
				else
				{
					num20++;
				}
			}
		}
		else
		{
			if (num7 == 0)
			{
				return;
			}
			if (num6 < 0)
			{
				int num22 = x1;
				x1 = x2;
				x2 = num22;
				int num23 = y1;
				y1 = y2;
				y2 = num23;
			}
			int num24 = x1 << 8;
			int num25 = x2 << 8;
			int num26 = pixelWidth << 8;
			int num27 = (num5 << 8) / num6;
			if (x1 < x2)
			{
				if (x1 >= num2 || x2 < num)
				{
					return;
				}
				if (num24 < 0)
				{
					if (num27 == 0)
					{
						return;
					}
					int num28 = num24;
					num24 = num27 - 1 + (num24 + 1) % num27;
					y1 += (num24 - num28) / num27;
				}
				if (num25 >= num26 && num27 != 0)
				{
					num25 = num26 - 1 - (num26 - 1 - num24) % num27;
					y2 = y1 + (num25 - num24) / num27;
				}
			}
			else
			{
				if (x2 >= num2 || x1 < num)
				{
					return;
				}
				if (num24 >= num26)
				{
					if (num27 == 0)
					{
						return;
					}
					int num29 = num24;
					num24 = num26 - 1 + (num27 - (num26 - 1 - num29) % num27);
					y1 += (num24 - num29) / num27;
				}
				if (num25 < 0 && num27 != 0)
				{
					num25 = num24 % num27;
					y2 = y1 + (num25 - num24) / num27;
				}
			}
			if (y1 < 0)
			{
				num24 -= num27 * y1;
				y1 = 0;
			}
			if (y2 >= pixelHeight)
			{
				y2 = pixelHeight - 1;
			}
			long num30 = num24;
			int num31 = y1 * pixelWidth;
			int num32 = (pixelWidth << 8) + num27;
			for (int j = y1; j <= y2; j++)
			{
				pixels[num31 + (num30 >> 8)] = color;
				num30 += num32;
			}
		}
	}

	public static void DrawLinePenned(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, WriteableBitmap penBmp, Rect? clipRect = null)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		using BitmapContext pen = penBmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		DrawLinePenned(context, context.Width, context.Height, x1, y1, x2, y2, pen, clipRect);
	}

	public static void DrawLinePenned(BitmapContext context, int w, int h, int x1, int y1, int x2, int y2, BitmapContext pen, Rect? clipRect = null)
	{
		if ((y1 < 0 && y2 < 0) || (y1 > h && y2 > h) || (x1 == x2 && y1 == y2) || !CohenSutherlandLineClip(clipRect ?? new Rect(0.0, 0.0, w, h), ref x1, ref y1, ref x2, ref y2))
		{
			return;
		}
		int width = pen.Width;
		int sourceWidth = width;
		Rect sourceRect = new Rect(0.0, 0.0, width, width);
		int num = x2 - x1;
		int num2 = y2 - y1;
		int num3 = 0;
		if (num < 0)
		{
			num = -num;
			num3 = -1;
		}
		else if (num > 0)
		{
			num3 = 1;
		}
		int num4 = 0;
		if (num2 < 0)
		{
			num2 = -num2;
			num4 = -1;
		}
		else if (num2 > 0)
		{
			num4 = 1;
		}
		int num5;
		int num6;
		int num7;
		int num8;
		int num9;
		int num10;
		if (num > num2)
		{
			num5 = num3;
			num6 = 0;
			num7 = num3;
			num8 = num4;
			num9 = num2;
			num10 = num;
		}
		else
		{
			num5 = 0;
			num6 = num4;
			num7 = num3;
			num8 = num4;
			num9 = num;
			num10 = num2;
		}
		int num11 = x1;
		int num12 = y1;
		int num13 = num10 >> 1;
		Rect destRect = new Rect(num11, num12, width, width);
		if (num12 < h && num12 >= 0 && num11 < w && num11 >= 0)
		{
			Blit(context, w, h, destRect, pen, sourceRect, sourceWidth);
		}
		for (int i = 0; i < num10; i++)
		{
			num13 -= num9;
			if (num13 < 0)
			{
				num13 += num10;
				num11 += num7;
				num12 += num8;
			}
			else
			{
				num11 += num5;
				num12 += num6;
			}
			if (num12 < h && num12 >= 0 && num11 < w && num11 >= 0)
			{
				Blit(context, w, h, new Rect(num11, num12, width, width), pen, sourceRect, sourceWidth);
			}
		}
	}

	private static byte ComputeOutCode(Rect extents, double x, double y)
	{
		byte b = 0;
		if (x < extents.Left)
		{
			b |= 1;
		}
		else if (x > extents.Right)
		{
			b |= 2;
		}
		if (y > extents.Bottom)
		{
			b |= 4;
		}
		else if (y < extents.Top)
		{
			b |= 8;
		}
		return b;
	}

	public static void DrawLineDotted(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int dotSpace, int dotLength, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawLineDotted(x1, y1, x2, y2, dotSpace, dotLength, color2);
	}

	public static void DrawLineDotted(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int dotSpace, int dotLength, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		if (x1 == x2)
		{
			SwapHorV(ref y1, ref y2);
			DrawVertically(context, x1, y1, y2, dotSpace, dotLength, color);
		}
		else if (y1 == y2)
		{
			SwapHorV(ref x1, ref x2);
			DrawHorizontally(context, x1, x2, y1, dotSpace, dotLength, color);
		}
		else
		{
			Draw(context, x1, y1, x2, y2, dotSpace, dotLength, color);
		}
	}

	private unsafe static void DrawVertically(BitmapContext context, int x, int y1, int y2, int dotSpace, int dotLength, int color)
	{
		int width = context.Width;
		int height = context.Height;
		if (x < 0 || x > width)
		{
			return;
		}
		int* pixels = context.Pixels;
		bool flag = true;
		int num = 0;
		for (int i = y1; i <= y2; i++)
		{
			if (i >= 1)
			{
				if (i >= height)
				{
					break;
				}
				if (flag)
				{
					int num2 = (i - 1) * width + x;
					pixels[num2] = color;
					flag = i % dotLength != 0;
					num = 0;
				}
				else
				{
					num++;
					flag = num % dotSpace == 0;
				}
			}
		}
	}

	private unsafe static void DrawHorizontally(BitmapContext context, int x1, int x2, int y, int dotSpace, int dotLength, int color)
	{
		int width = context.Width;
		int height = context.Height;
		if (y < 0 || y > height)
		{
			return;
		}
		int* pixels = context.Pixels;
		bool flag = true;
		int num = 0;
		for (int i = x1; i <= x2; i++)
		{
			if (i >= 1)
			{
				if (i >= width || y >= height)
				{
					break;
				}
				if (flag)
				{
					int num2 = y * width + i - 1;
					pixels[num2] = color;
					flag = i % dotLength != 0;
					num = 0;
				}
				else
				{
					num++;
					flag = num % dotSpace == 0;
				}
			}
		}
	}

	private unsafe static void Draw(BitmapContext context, int x1, int y1, int x2, int y2, int dotSpace, int dotLength, int color)
	{
		int width = context.Width;
		int height = context.Height;
		if (!CohenSutherlandLineClip(new Rect(0.0, 0.0, width, height), ref x1, ref y1, ref x2, ref y2))
		{
			return;
		}
		Swap(ref x1, ref x2, ref y1, ref y2);
		float num = (float)(y2 - y1) / (float)(x2 - x1);
		float num2 = (float)y1 - num * (float)x1;
		int* pixels = context.Pixels;
		bool flag = true;
		int num3 = 0;
		for (int i = x1; i <= width; i++)
		{
			if (i == 0)
			{
				continue;
			}
			int num4 = (int)(num * (float)i + num2);
			if (num4 > 0 && num4 < height && i < x2)
			{
				if (flag)
				{
					int num5 = (num4 - 1) * width + i - 1;
					pixels[num5] = color;
					num3 = 0;
					flag = i % dotLength != 0;
				}
				else
				{
					num3++;
					flag = num3 % dotSpace == 0;
				}
			}
		}
	}

	private static void Swap(ref int x1, ref int x2, ref int y1, ref int y2)
	{
		if (x2 < x1)
		{
			int num = x1;
			int num2 = x2;
			int num3 = y1;
			int num4 = y2;
			x1 = num2;
			y1 = num4;
			x2 = num;
			y2 = num3;
		}
	}

	private static void SwapHorV(ref int a1, ref int a2)
	{
		int x = 0;
		int x2 = -1;
		if (a2 < a1)
		{
			Swap(ref x, ref x2, ref a1, ref a2);
		}
	}

	public static void DrawLineWu(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int sa, int sr, int sg, int sb, Rect? clipRect = null)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		DrawLineWu(context, context.Width, context.Height, x1, y1, x2, y2, sa, sr, sg, sb, clipRect);
	}

	public unsafe static void DrawLineWu(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int sa, int sr, int sg, int sb, Rect? clipRect = null)
	{
		if (!CohenSutherlandLineClip(clipRect ?? new Rect(0.0, 0.0, pixelWidth, pixelHeight), ref x1, ref y1, ref x2, ref y2))
		{
			return;
		}
		int* pixels = context.Pixels;
		if (y1 > y2)
		{
			int num = y1;
			y1 = y2;
			y2 = num;
			int num2 = x1;
			x1 = x2;
			x2 = num2;
		}
		pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
		short num3 = (short)(x2 - x1);
		short num4;
		if (num3 >= 0)
		{
			num4 = 1;
		}
		else
		{
			num4 = -1;
			num3 = (short)(-num3);
		}
		short num5 = (short)(y2 - y1);
		if (num5 == 0)
		{
			while (num3-- != 0)
			{
				x1 += num4;
				pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
			}
			return;
		}
		if (num3 == 0)
		{
			do
			{
				y1++;
				pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
			}
			while (--num5 != 0);
			return;
		}
		if (num3 == num5)
		{
			do
			{
				x1 += num4;
				y1++;
				pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr, sg, sb, pixels[y1 * pixelWidth + x1]);
			}
			while (--num5 != 0);
			return;
		}
		ushort num6 = 0;
		ushort num7;
		if (num5 > num3)
		{
			num7 = (ushort)((ulong)((long)num3 << 16) / (ulong)num5);
			while (--num5 != 0)
			{
				ushort num8 = num6;
				num6 += num7;
				if (num6 <= num8)
				{
					x1 += num4;
				}
				y1++;
				ushort num9 = (ushort)(num6 >> 8);
				int num10 = num9 ^ 0xFF;
				pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr * num10 >> 8, sg * num10 >> 8, sb * num10 >> 8, pixels[y1 * pixelWidth + x1]);
				num10 = num9;
				pixels[y1 * pixelWidth + x1 + num4] = AlphaBlend(sa, sr * num10 >> 8, sg * num10 >> 8, sb * num10 >> 8, pixels[y1 * pixelWidth + x1 + num4]);
			}
			pixels[y2 * pixelWidth + x2] = AlphaBlend(sa, sr, sg, sb, pixels[y2 * pixelWidth + x2]);
			return;
		}
		num7 = (ushort)((ulong)((long)num5 << 16) / (ulong)num3);
		while (--num3 != 0)
		{
			ushort num8 = num6;
			num6 += num7;
			if (num6 <= num8)
			{
				y1++;
			}
			x1 += num4;
			ushort num11 = (ushort)(num6 >> 8);
			int num12 = num11 ^ 0xFF;
			pixels[y1 * pixelWidth + x1] = AlphaBlend(sa, sr * num12 >> 8, sg * num12 >> 8, sb * num12 >> 8, pixels[y1 * pixelWidth + x1]);
			num12 = num11;
			pixels[(y1 + 1) * pixelWidth + x1] = AlphaBlend(sa, sr * num12 >> 8, sg * num12 >> 8, sb * num12 >> 8, pixels[(y1 + 1) * pixelWidth + x1]);
		}
		pixels[y2 * pixelWidth + x2] = AlphaBlend(sa, sr, sg, sb, pixels[y2 * pixelWidth + x2]);
	}

	public static void DrawLineAa(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int color, int strokeThickness, Rect? clipRect = null)
	{
		AAWidthLine(pixelWidth, pixelHeight, context, x1, y1, x2, y2, strokeThickness, color, clipRect);
	}

	public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, int strokeThickness, Rect? clipRect = null)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		AAWidthLine(context.Width, context.Height, context, x1, y1, x2, y2, strokeThickness, color, clipRect);
	}

	public static void DrawLineAa(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, Color color, int strokeThickness, Rect? clipRect = null)
	{
		int color2 = ConvertColor(color);
		AAWidthLine(pixelWidth, pixelHeight, context, x1, y1, x2, y2, strokeThickness, color2, clipRect);
	}

	public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, int strokeThickness, Rect? clipRect = null)
	{
		int color2 = ConvertColor(color);
		using BitmapContext context = bmp.GetBitmapContext();
		AAWidthLine(context.Width, context.Height, context, x1, y1, x2, y2, strokeThickness, color2, clipRect);
	}

	public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color, Rect? clipRect = null)
	{
		int color2 = ConvertColor(color);
		bmp.DrawLineAa(x1, y1, x2, y2, color2, clipRect);
	}

	public static void DrawLineAa(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		DrawLineAa(context, context.Width, context.Height, x1, y1, x2, y2, color, clipRect);
	}

	public static void DrawLineAa(BitmapContext context, int pixelWidth, int pixelHeight, int x1, int y1, int x2, int y2, int color, Rect? clipRect = null)
	{
		if ((x1 == x2 && y1 == y2) || !CohenSutherlandLineClip(clipRect ?? new Rect(0.0, 0.0, pixelWidth, pixelHeight), ref x1, ref y1, ref x2, ref y2))
		{
			return;
		}
		if (x1 < 1)
		{
			x1 = 1;
		}
		if (x1 > pixelWidth - 2)
		{
			x1 = pixelWidth - 2;
		}
		if (y1 < 1)
		{
			y1 = 1;
		}
		if (y1 > pixelHeight - 2)
		{
			y1 = pixelHeight - 2;
		}
		if (x2 < 1)
		{
			x2 = 1;
		}
		if (x2 > pixelWidth - 2)
		{
			x2 = pixelWidth - 2;
		}
		if (y2 < 1)
		{
			y2 = 1;
		}
		if (y2 > pixelHeight - 2)
		{
			y2 = pixelHeight - 2;
		}
		int num = y1 * pixelWidth + x1;
		int num2 = x2 - x1;
		int num3 = y2 - y1;
		int num4 = (color >> 24) & 0xFF;
		uint srb = (uint)(color & 0xFF00FF);
		uint sg = (uint)((color >> 8) & 0xFF);
		int num5 = num2;
		int num6 = num3;
		if (num2 < 0)
		{
			num5 = -num2;
		}
		if (num3 < 0)
		{
			num6 = -num3;
		}
		int num7;
		int num8;
		int num9;
		int num10;
		int num11;
		int num12;
		if (num5 > num6)
		{
			num7 = num5;
			num8 = num6;
			num9 = x2;
			num10 = y2;
			num11 = 1;
			num12 = pixelWidth;
			if (num2 < 0)
			{
				num11 = -num11;
			}
			if (num3 < 0)
			{
				num12 = -num12;
			}
		}
		else
		{
			num7 = num6;
			num8 = num5;
			num9 = y2;
			num10 = x2;
			num11 = pixelWidth;
			num12 = 1;
			if (num3 < 0)
			{
				num11 = -num11;
			}
			if (num2 < 0)
			{
				num12 = -num12;
			}
		}
		int num13 = num9 + num7;
		int num14 = (num8 << 1) - num7;
		int num15 = num8 << 1;
		int num16 = num8 - num7 << 1;
		double num17 = 1.0 / (4.0 * Math.Sqrt(num7 * num7 + num8 * num8));
		double num18 = 0.75 - 2.0 * ((double)num7 * num17);
		int num19 = (int)(num17 * 1024.0);
		int num20 = (int)(num18 * 1024.0 * (double)num4);
		int num21 = (int)(768.0 * (double)num4);
		int num22 = num19 * num4;
		int num23 = num7 * num22;
		int num24 = num14 * num22;
		int num25 = 0;
		int num26 = num15 * num22;
		int num27 = num16 * num22;
		do
		{
			AlphaBlendNormalOnPremultiplied(context, num, num21 - num25 >> 10, srb, sg);
			AlphaBlendNormalOnPremultiplied(context, num + num12, num20 + num25 >> 10, srb, sg);
			AlphaBlendNormalOnPremultiplied(context, num - num12, num20 - num25 >> 10, srb, sg);
			if (num14 < 0)
			{
				num25 = num24 + num23;
				num14 += num15;
				num24 += num26;
			}
			else
			{
				num25 = num24 - num23;
				num14 += num16;
				num24 += num27;
				num10++;
				num += num12;
			}
			num9++;
			num += num11;
		}
		while (num9 <= num13);
	}

	private unsafe static void AlphaBlendNormalOnPremultiplied(BitmapContext context, int index, int sa, uint srb, uint sg)
	{
		int* pixels = context.Pixels;
		int num = pixels[index];
		uint num2 = (uint)num >> 24;
		uint num3 = (uint)((num >>> 8) & 0xFF);
		uint num4 = (uint)(num & 0xFF00FF);
		pixels[index] = (int)((sa + (num2 * (255 - sa) * 32897 >> 23) << 24) | (((sg - num3) * sa + (num3 << 8)) & 0xFFFFFF00u) | ((((srb - num4) * sa >> 8) + num4) & 0xFF00FF));
	}

	internal static bool CohenSutherlandLineClipWithViewPortOffset(Rect viewPort, ref float xi0, ref float yi0, ref float xi1, ref float yi1, int offset)
	{
		return CohenSutherlandLineClip(new Rect(viewPort.X - (double)offset, viewPort.Y - (double)offset, viewPort.Width + (double)(2 * offset), viewPort.Height + (double)(2 * offset)), ref xi0, ref yi0, ref xi1, ref yi1);
	}

	internal static bool CohenSutherlandLineClip(Rect extents, ref float xi0, ref float yi0, ref float xi1, ref float yi1)
	{
		double x = ClipToInt(xi0);
		double y = ClipToInt(yi0);
		double x2 = ClipToInt(xi1);
		double y2 = ClipToInt(yi1);
		bool result = CohenSutherlandLineClip(extents, ref x, ref y, ref x2, ref y2);
		xi0 = (float)x;
		yi0 = (float)y;
		xi1 = (float)x2;
		yi1 = (float)y2;
		return result;
	}

	private static float ClipToInt(float d)
	{
		if (d > 2.1474836E+09f)
		{
			return 2.1474836E+09f;
		}
		if (d < -2.1474836E+09f)
		{
			return -2.1474836E+09f;
		}
		return d;
	}

	internal static bool CohenSutherlandLineClip(Rect extents, ref int xi0, ref int yi0, ref int xi1, ref int yi1)
	{
		double x = xi0;
		double y = yi0;
		double x2 = xi1;
		double y2 = yi1;
		bool result = CohenSutherlandLineClip(extents, ref x, ref y, ref x2, ref y2);
		xi0 = (int)x;
		yi0 = (int)y;
		xi1 = (int)x2;
		yi1 = (int)y2;
		return result;
	}

	internal static bool CohenSutherlandLineClip(Rect extents, ref double x0, ref double y0, ref double x1, ref double y1)
	{
		byte b = ComputeOutCode(extents, x0, y0);
		byte b2 = ComputeOutCode(extents, x1, y1);
		if (b == 0 && b2 == 0)
		{
			return true;
		}
		bool result = false;
		while (true)
		{
			if ((b | b2) == 0)
			{
				result = true;
				break;
			}
			if ((b & b2) != 0)
			{
				break;
			}
			byte b3 = ((b != 0) ? b : b2);
			double num;
			double num2;
			if ((b3 & 8) != 0)
			{
				num = x0 + (x1 - x0) * (extents.Top - y0) / (y1 - y0);
				num2 = extents.Top;
			}
			else if ((b3 & 4) != 0)
			{
				num = x0 + (x1 - x0) * (extents.Bottom - y0) / (y1 - y0);
				num2 = extents.Bottom;
			}
			else if ((b3 & 2) != 0)
			{
				num2 = y0 + (y1 - y0) * (extents.Right - x0) / (x1 - x0);
				num = extents.Right;
			}
			else if ((b3 & 1) != 0)
			{
				num2 = y0 + (y1 - y0) * (extents.Left - x0) / (x1 - x0);
				num = extents.Left;
			}
			else
			{
				num = double.NaN;
				num2 = double.NaN;
			}
			if (b3 == b)
			{
				x0 = num;
				y0 = num2;
				b = ComputeOutCode(extents, x0, y0);
			}
			else
			{
				x1 = num;
				y1 = num2;
				b2 = ComputeOutCode(extents, x1, y1);
			}
		}
		return result;
	}

	public static int AlphaBlend(int sa, int sr, int sg, int sb, int destPixel)
	{
		int num = (destPixel >> 24) & 0xFF;
		int num2 = (destPixel >> 16) & 0xFF;
		int num3 = (destPixel >> 8) & 0xFF;
		int num4 = destPixel & 0xFF;
		destPixel = (sa + (num * (255 - sa) * 32897 >> 23) << 24) | (sr + (num2 * (255 - sa) * 32897 >> 23) << 16) | (sg + (num3 * (255 - sa) * 32897 >> 23) << 8) | (sb + (num4 * (255 - sa) * 32897 >> 23));
		return destPixel;
	}

	public static void DrawPolyline(this WriteableBitmap bmp, int[] points, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawPolyline(points, color2);
	}

	public static void DrawPolyline(this WriteableBitmap bmp, int[] points, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		int x = points[0];
		int y = points[1];
		for (int i = 2; i < points.Length; i += 2)
		{
			int num = points[i];
			int num2 = points[i + 1];
			DrawLine(context, width, height, x, y, num, num2, color);
			x = num;
			y = num2;
		}
	}

	public static void DrawPolylineAa(this WriteableBitmap bmp, int[] points, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawPolylineAa(points, color2);
	}

	public static void DrawPolylineAa(this WriteableBitmap bmp, int[] points, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		int x = points[0];
		int y = points[1];
		for (int i = 2; i < points.Length; i += 2)
		{
			int num = points[i];
			int num2 = points[i + 1];
			DrawLineAa(context, width, height, x, y, num, num2, color);
			x = num;
			y = num2;
		}
	}

	public static void DrawTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawTriangle(x1, y1, x2, y2, x3, y3, color2);
	}

	public static void DrawTriangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		DrawLine(context, width, height, x1, y1, x2, y2, color);
		DrawLine(context, width, height, x2, y2, x3, y3, color);
		DrawLine(context, width, height, x3, y3, x1, y1, color);
	}

	public static void DrawQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawQuad(x1, y1, x2, y2, x3, y3, x4, y4, color2);
	}

	public static void DrawQuad(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		DrawLine(context, width, height, x1, y1, x2, y2, color);
		DrawLine(context, width, height, x2, y2, x3, y3, color);
		DrawLine(context, width, height, x3, y3, x4, y4, color);
		DrawLine(context, width, height, x4, y4, x1, y1, color);
	}

	public static void DrawRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawRectangle(x1, y1, x2, y2, color2);
	}

	public unsafe static void DrawRectangle(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		if ((x1 >= 0 || x2 >= 0) && (y1 >= 0 || y2 >= 0) && (x1 < width || x2 < width) && (y1 < height || y2 < height))
		{
			if (x1 < 0)
			{
				x1 = 0;
			}
			if (y1 < 0)
			{
				y1 = 0;
			}
			if (x2 < 0)
			{
				x2 = 0;
			}
			if (y2 < 0)
			{
				y2 = 0;
			}
			if (x1 >= width)
			{
				x1 = width - 1;
			}
			if (y1 >= height)
			{
				y1 = height - 1;
			}
			if (x2 >= width)
			{
				x2 = width - 1;
			}
			if (y2 >= height)
			{
				y2 = height - 1;
			}
			int num = y1 * width;
			int num2 = y2 * width + x1;
			int num3 = num + x2;
			int num4 = num + x1;
			for (int i = num4; i <= num3; i++)
			{
				pixels[i] = color;
				pixels[num2] = color;
				num2++;
			}
			num3 = num4 + width;
			num2 -= width;
			for (int j = num + x2 + width; j <= num2; j += width)
			{
				pixels[j] = color;
				pixels[num3] = color;
				num3 += width;
			}
		}
	}

	public static void DrawEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawEllipse(x1, y1, x2, y2, color2);
	}

	public static void DrawEllipse(this WriteableBitmap bmp, int x1, int y1, int x2, int y2, int color)
	{
		int num = x2 - x1 >> 1;
		int num2 = y2 - y1 >> 1;
		int xc = x1 + num;
		int yc = y1 + num2;
		bmp.DrawEllipseCentered(xc, yc, num, num2, color);
	}

	public static void DrawEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawEllipseCentered(xc, yc, xr, yr, color2);
	}

	public unsafe static void DrawEllipseCentered(this WriteableBitmap bmp, int xc, int yc, int xr, int yr, int color)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext();
		int* pixels = bitmapContext.Pixels;
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		if (xr < 1 || yr < 1)
		{
			return;
		}
		int num = xr;
		int num2 = 0;
		int num3 = xr * xr << 1;
		int num4 = yr * yr << 1;
		int num5 = yr * yr * (1 - (xr << 1));
		int num6 = xr * xr;
		int num7 = 0;
		int num8 = num4 * xr;
		int num9 = 0;
		int num10;
		int num11;
		int num14;
		int num15;
		while (num8 >= num9)
		{
			num10 = yc + num2;
			num11 = yc - num2;
			int num12 = xc + num;
			int num13 = xc - num;
			if (0 <= num10 && num10 < height)
			{
				num14 = num10 * width;
				if (0 <= num12 && num12 < width)
				{
					pixels[num12 + num14] = color;
				}
				if (0 <= num13 && num13 < width)
				{
					pixels[num13 + num14] = color;
				}
			}
			if (0 <= num11 && num11 < height)
			{
				num15 = num11 * width;
				if (0 <= num13 && num13 < width)
				{
					pixels[num13 + num15] = color;
				}
				if (0 <= num12 && num12 < width)
				{
					pixels[num12 + num15] = color;
				}
			}
			num2++;
			num9 += num3;
			num7 += num6;
			num6 += num3;
			if (num5 + (num7 << 1) > 0)
			{
				num--;
				num8 -= num4;
				num7 += num5;
				num5 += num4;
			}
		}
		num = 0;
		num2 = yr;
		num10 = yc + num2;
		num11 = yc - num2;
		num14 = num10 * width;
		num15 = num11 * width;
		num5 = yr * yr;
		num6 = xr * xr * (1 - (yr << 1));
		num7 = 0;
		num8 = 0;
		num9 = num3 * yr;
		while (num8 <= num9)
		{
			int num12 = xc + num;
			if (0 <= num12 && num12 < width)
			{
				if (0 <= num10 && num10 < height)
				{
					pixels[num12 + num14] = color;
				}
				if (0 <= num11 && num11 < height)
				{
					pixels[num12 + num15] = color;
				}
			}
			int num13 = xc - num;
			if (0 <= num13 && num13 < width)
			{
				if (0 <= num10 && num10 < height)
				{
					pixels[num13 + num14] = color;
				}
				if (0 <= num11 && num11 < height)
				{
					pixels[num13 + num15] = color;
				}
			}
			num++;
			num8 += num4;
			num7 += num5;
			num5 += num4;
			if (num6 + (num7 << 1) > 0)
			{
				num2--;
				num10 = yc + num2;
				num11 = yc - num2;
				num14 = num10 * width;
				num15 = num11 * width;
				num9 -= num3;
				num7 += num6;
				num6 += num3;
			}
		}
	}

	public static void DrawBezier(this WriteableBitmap bmp, int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawBezier(x1, y1, cx1, cy1, cx2, cy2, x2, y2, color2);
	}

	public static void DrawBezier(this WriteableBitmap bmp, int x1, int y1, int cx1, int cy1, int cx2, int cy2, int x2, int y2, int color)
	{
		int num = Math.Min(x1, Math.Min(cx1, Math.Min(cx2, x2)));
		int num2 = Math.Min(y1, Math.Min(cy1, Math.Min(cy2, y2)));
		int num3 = Math.Max(x1, Math.Max(cx1, Math.Max(cx2, x2)));
		int num4 = Math.Max(y1, Math.Max(cy1, Math.Max(cy2, y2)));
		int num5 = num3 - num;
		int num6 = num4 - num2;
		if (num5 > num6)
		{
			num6 = num5;
		}
		if (num6 == 0)
		{
			return;
		}
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		float num7 = 2f / (float)num6;
		int x3 = x1;
		int y3 = y1;
		for (float num8 = num7; num8 <= 1f; num8 += num7)
		{
			float num9 = num8 * num8;
			float num10 = 1f - num8;
			float num11 = num10 * num10;
			int num12 = (int)(num10 * num11 * (float)x1 + 3f * num8 * num11 * (float)cx1 + 3f * num10 * num9 * (float)cx2 + num8 * num9 * (float)x2);
			int num13 = (int)(num10 * num11 * (float)y1 + 3f * num8 * num11 * (float)cy1 + 3f * num10 * num9 * (float)cy2 + num8 * num9 * (float)y2);
			DrawLine(context, width, height, x3, y3, num12, num13, color);
			x3 = num12;
			y3 = num13;
		}
		DrawLine(context, width, height, x3, y3, x2, y2, color);
	}

	public static void DrawBeziers(this WriteableBitmap bmp, int[] points, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawBeziers(points, color2);
	}

	public static void DrawBeziers(this WriteableBitmap bmp, int[] points, int color)
	{
		int x = points[0];
		int y = points[1];
		for (int i = 2; i + 5 < points.Length; i += 6)
		{
			int num = points[i + 4];
			int num2 = points[i + 5];
			bmp.DrawBezier(x, y, points[i], points[i + 1], points[i + 2], points[i + 3], num, num2, color);
			x = num;
			y = num2;
		}
	}

	private static void DrawCurveSegment(int x1, int y1, int x2, int y2, int x3, int y3, int x4, int y4, float tension, int color, BitmapContext context, int w, int h)
	{
		int num = Math.Min(x1, Math.Min(x2, Math.Min(x3, x4)));
		int num2 = Math.Min(y1, Math.Min(y2, Math.Min(y3, y4)));
		int num3 = Math.Max(x1, Math.Max(x2, Math.Max(x3, x4)));
		int num4 = Math.Max(y1, Math.Max(y2, Math.Max(y3, y4)));
		int num5 = num3 - num;
		int num6 = num4 - num2;
		if (num5 > num6)
		{
			num6 = num5;
		}
		if (num6 != 0)
		{
			float num7 = 2f / (float)num6;
			int x5 = x2;
			int y5 = y2;
			float num8 = tension * (float)(x3 - x1);
			float num9 = tension * (float)(y3 - y1);
			float num10 = tension * (float)(x4 - x2);
			float num11 = tension * (float)(y4 - y2);
			float num12 = num8 + num10 + (float)(2 * x2) - (float)(2 * x3);
			float num13 = num9 + num11 + (float)(2 * y2) - (float)(2 * y3);
			float num14 = -2f * num8 - num10 - (float)(3 * x2) + (float)(3 * x3);
			float num15 = -2f * num9 - num11 - (float)(3 * y2) + (float)(3 * y3);
			for (float num16 = num7; num16 <= 1f; num16 += num7)
			{
				float num17 = num16 * num16;
				int num18 = (int)(num12 * num17 * num16 + num14 * num17 + num8 * num16 + (float)x2);
				int num19 = (int)(num13 * num17 * num16 + num15 * num17 + num9 * num16 + (float)y2);
				DrawLine(context, w, h, x5, y5, num18, num19, color);
				x5 = num18;
				y5 = num19;
			}
			DrawLine(context, w, h, x5, y5, x3, y3, color);
		}
	}

	public static void DrawCurve(this WriteableBitmap bmp, int[] points, float tension, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawCurve(points, tension, color2);
	}

	public static void DrawCurve(this WriteableBitmap bmp, int[] points, float tension, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		DrawCurveSegment(points[0], points[1], points[0], points[1], points[2], points[3], points[4], points[5], tension, color, context, width, height);
		int i;
		for (i = 2; i < points.Length - 4; i += 2)
		{
			DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension, color, context, width, height);
		}
		DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 2], points[i + 3], tension, color, context, width, height);
	}

	public static void DrawCurveClosed(this WriteableBitmap bmp, int[] points, float tension, Color color)
	{
		int color2 = ConvertColor(color);
		bmp.DrawCurveClosed(points, tension, color2);
	}

	public static void DrawCurveClosed(this WriteableBitmap bmp, int[] points, float tension, int color)
	{
		using BitmapContext context = bmp.GetBitmapContext();
		int width = context.Width;
		int height = context.Height;
		int num = points.Length;
		DrawCurveSegment(points[num - 2], points[num - 1], points[0], points[1], points[2], points[3], points[4], points[5], tension, color, context, width, height);
		int i;
		for (i = 2; i < num - 4; i += 2)
		{
			DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[i + 4], points[i + 5], tension, color, context, width, height);
		}
		DrawCurveSegment(points[i - 2], points[i - 1], points[i], points[i + 1], points[i + 2], points[i + 3], points[0], points[1], tension, color, context, width, height);
		DrawCurveSegment(points[i], points[i + 1], points[i + 2], points[i + 3], points[0], points[1], points[2], points[3], tension, color, context, width, height);
	}

	public static WriteableBitmap Crop(this WriteableBitmap bmp, int x, int y, int width, int height)
	{
		using BitmapContext src = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width2 = src.Width;
		int height2 = src.Height;
		if (x > width2 || y > height2)
		{
			return BitmapFactory.New(0, 0);
		}
		if (x < 0)
		{
			x = 0;
		}
		if (x + width > width2)
		{
			width = width2 - x;
		}
		if (y < 0)
		{
			y = 0;
		}
		if (y + height > height2)
		{
			height = height2 - y;
		}
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using BitmapContext dest = writeableBitmap.GetBitmapContext();
		for (int i = 0; i < height; i++)
		{
			int srcOffset = ((y + i) * width2 + x) * 4;
			int destOffset = i * width * 4;
			BitmapContext.BlockCopy(src, srcOffset, dest, destOffset, width * 4);
		}
		return writeableBitmap;
	}

	public static WriteableBitmap Crop(this WriteableBitmap bmp, Rect region)
	{
		return bmp.Crop((int)region.X, (int)region.Y, (int)region.Width, (int)region.Height);
	}

	public static WriteableBitmap Resize(this WriteableBitmap bmp, int width, int height, Interpolation interpolation)
	{
		using BitmapContext srcContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int[] array = Resize(srcContext, srcContext.Width, srcContext.Height, width, height, interpolation);
		WriteableBitmap writeableBitmap = BitmapFactory.New(width, height);
		using (BitmapContext dest = writeableBitmap.GetBitmapContext())
		{
			BitmapContext.BlockCopy(array, 0, dest, 0, 4 * array.Length);
		}
		return writeableBitmap;
	}

	public unsafe static int[] Resize(BitmapContext srcContext, int widthSource, int heightSource, int width, int height, Interpolation interpolation)
	{
		return Resize(srcContext.Pixels, widthSource, heightSource, width, height, interpolation);
	}

	public unsafe static int[] Resize(int* pixels, int widthSource, int heightSource, int width, int height, Interpolation interpolation)
	{
		int[] array = new int[width * height];
		float num = (float)widthSource / (float)width;
		float num2 = (float)heightSource / (float)height;
		switch (interpolation)
		{
		case Interpolation.NearestNeighbor:
		{
			int num23 = 0;
			for (int k = 0; k < height; k++)
			{
				for (int l = 0; l < width; l++)
				{
					float num4 = (float)l * num;
					float num24 = (float)k * num2;
					int num6 = (int)num4;
					int num7 = (int)num24;
					array[num23++] = pixels[num7 * widthSource + num6];
				}
			}
			break;
		}
		case Interpolation.Bilinear:
		{
			int num3 = 0;
			for (int i = 0; i < height; i++)
			{
				for (int j = 0; j < width; j++)
				{
					float num4 = (float)j * num;
					float num5 = (float)i * num2;
					int num6 = (int)num4;
					int num7 = (int)num5;
					float num8 = num4 - (float)num6;
					float num9 = num5 - (float)num7;
					float num10 = 1f - num8;
					float num11 = 1f - num9;
					int num12 = num6 + 1;
					if (num12 >= widthSource)
					{
						num12 = num6;
					}
					int num13 = num7 + 1;
					if (num13 >= heightSource)
					{
						num13 = num7;
					}
					int num14 = pixels[num7 * widthSource + num6];
					byte b = (byte)(num14 >> 24);
					byte b2 = (byte)(num14 >> 16);
					byte b3 = (byte)(num14 >> 8);
					byte b4 = (byte)num14;
					int num15 = pixels[num7 * widthSource + num12];
					byte b5 = (byte)(num15 >> 24);
					byte b6 = (byte)(num15 >> 16);
					byte b7 = (byte)(num15 >> 8);
					byte b8 = (byte)num15;
					int num16 = pixels[num13 * widthSource + num6];
					byte b9 = (byte)(num16 >> 24);
					byte b10 = (byte)(num16 >> 16);
					byte b11 = (byte)(num16 >> 8);
					byte b12 = (byte)num16;
					int num17 = pixels[num13 * widthSource + num12];
					byte b13 = (byte)(num17 >> 24);
					byte b14 = (byte)(num17 >> 16);
					byte b15 = (byte)(num17 >> 8);
					byte b16 = (byte)num17;
					float num18 = num10 * (float)(int)b + num8 * (float)(int)b5;
					float num19 = num10 * (float)(int)b9 + num8 * (float)(int)b13;
					byte b17 = (byte)(num11 * num18 + num9 * num19);
					num18 = num10 * (float)(int)b2 + num8 * (float)(int)b6;
					num19 = num10 * (float)(int)b10 + num8 * (float)(int)b14;
					float num20 = num11 * num18 + num9 * num19;
					num18 = num10 * (float)(int)b3 + num8 * (float)(int)b7;
					num19 = num10 * (float)(int)b11 + num8 * (float)(int)b15;
					float num21 = num11 * num18 + num9 * num19;
					num18 = num10 * (float)(int)b4 + num8 * (float)(int)b8;
					num19 = num10 * (float)(int)b12 + num8 * (float)(int)b16;
					float num22 = num11 * num18 + num9 * num19;
					byte b18 = (byte)num20;
					byte b19 = (byte)num21;
					byte b20 = (byte)num22;
					array[num3++] = (b17 << 24) | (b18 << 16) | (b19 << 8) | b20;
				}
			}
			break;
		}
		}
		return array;
	}

	public unsafe static WriteableBitmap Rotate(this WriteableBitmap bmp, int angle)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		int num = 0;
		WriteableBitmap writeableBitmap = null;
		angle %= 360;
		if (angle > 0 && angle <= 90)
		{
			writeableBitmap = BitmapFactory.New(height, width);
			using BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext();
			int* pixels2 = bitmapContext2.Pixels;
			for (int i = 0; i < width; i++)
			{
				for (int num2 = height - 1; num2 >= 0; num2--)
				{
					int num3 = num2 * width + i;
					pixels2[num] = pixels[num3];
					num++;
				}
			}
		}
		else if (angle > 90 && angle <= 180)
		{
			writeableBitmap = BitmapFactory.New(width, height);
			using BitmapContext bitmapContext3 = writeableBitmap.GetBitmapContext();
			int* pixels3 = bitmapContext3.Pixels;
			for (int num4 = height - 1; num4 >= 0; num4--)
			{
				for (int num5 = width - 1; num5 >= 0; num5--)
				{
					int num6 = num4 * width + num5;
					pixels3[num] = pixels[num6];
					num++;
				}
			}
		}
		else if (angle > 180 && angle <= 270)
		{
			writeableBitmap = BitmapFactory.New(height, width);
			using BitmapContext bitmapContext4 = writeableBitmap.GetBitmapContext();
			int* pixels4 = bitmapContext4.Pixels;
			for (int num7 = width - 1; num7 >= 0; num7--)
			{
				for (int j = 0; j < height; j++)
				{
					int num8 = j * width + num7;
					pixels4[num] = pixels[num8];
					num++;
				}
			}
		}
		else
		{
			writeableBitmap = Clone(bmp);
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap RotateFree(this WriteableBitmap bmp, double angle, bool crop = true)
	{
		double num = -Math.PI / 180.0 * angle;
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int num2;
		int num3;
		if (crop)
		{
			num2 = width;
			num3 = height;
		}
		else
		{
			double num4 = angle / (180.0 / Math.PI);
			num2 = (int)Math.Ceiling(Math.Abs(Math.Sin(num4) * (double)height) + Math.Abs(Math.Cos(num4) * (double)width));
			num3 = (int)Math.Ceiling(Math.Abs(Math.Sin(num4) * (double)width) + Math.Abs(Math.Cos(num4) * (double)height));
		}
		int num5 = width / 2;
		int num6 = height / 2;
		int num7 = num2 / 2;
		int num8 = num3 / 2;
		WriteableBitmap writeableBitmap = BitmapFactory.New(num2, num3);
		using BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext();
		int* pixels = bitmapContext2.Pixels;
		int* pixels2 = bitmapContext.Pixels;
		int width2 = bitmapContext.Width;
		for (int i = 0; i < num3; i++)
		{
			for (int j = 0; j < num2; j++)
			{
				int num9 = j - num7;
				int num10 = num8 - i;
				double num11 = Math.Sqrt(num9 * num9 + num10 * num10);
				double num12;
				if (num9 == 0)
				{
					if (num10 == 0)
					{
						pixels[i * num2 + j] = pixels2[num6 * width2 + num5];
						continue;
					}
					num12 = ((num10 >= 0) ? (Math.PI / 2.0) : 4.71238898038469);
				}
				else
				{
					num12 = Math.Atan2(num10, num9);
				}
				num12 -= num;
				double num13 = num11 * Math.Cos(num12);
				double num14 = num11 * Math.Sin(num12);
				num13 += (double)num5;
				num14 = (double)num6 - num14;
				int num15 = (int)Math.Floor(num13);
				int num16 = (int)Math.Floor(num14);
				int num17 = (int)Math.Ceiling(num13);
				int num18 = (int)Math.Ceiling(num14);
				if (num15 >= 0 && num17 >= 0 && num15 < width && num17 < width && num16 >= 0 && num18 >= 0 && num16 < height && num18 < height)
				{
					double num19 = num13 - (double)num15;
					double num20 = num14 - (double)num16;
					int num21 = pixels2[num16 * width2 + num15];
					int num22 = pixels2[num16 * width2 + num17];
					int num23 = pixels2[num18 * width2 + num15];
					int num24 = pixels2[num18 * width2 + num17];
					double num25 = (1.0 - num19) * (double)((num21 >> 24) & 0xFF) + num19 * (double)((num22 >> 24) & 0xFF);
					double num26 = (1.0 - num19) * (double)((num21 >> 16) & 0xFF) + num19 * (double)((num22 >> 16) & 0xFF);
					double num27 = (1.0 - num19) * (double)((num21 >> 8) & 0xFF) + num19 * (double)((num22 >> 8) & 0xFF);
					double num28 = (1.0 - num19) * (double)(num21 & 0xFF) + num19 * (double)(num22 & 0xFF);
					double num29 = (1.0 - num19) * (double)((num23 >> 24) & 0xFF) + num19 * (double)((num24 >> 24) & 0xFF);
					double num30 = (1.0 - num19) * (double)((num23 >> 16) & 0xFF) + num19 * (double)((num24 >> 16) & 0xFF);
					double num31 = (1.0 - num19) * (double)((num23 >> 8) & 0xFF) + num19 * (double)((num24 >> 8) & 0xFF);
					double num32 = (1.0 - num19) * (double)(num23 & 0xFF) + num19 * (double)(num24 & 0xFF);
					int num33 = (int)Math.Round((1.0 - num20) * num26 + num20 * num30);
					int num34 = (int)Math.Round((1.0 - num20) * num27 + num20 * num31);
					int num35 = (int)Math.Round((1.0 - num20) * num28 + num20 * num32);
					int num36 = (int)Math.Round((1.0 - num20) * num25 + num20 * num29);
					if (num33 < 0)
					{
						num33 = 0;
					}
					if (num33 > 255)
					{
						num33 = 255;
					}
					if (num34 < 0)
					{
						num34 = 0;
					}
					if (num34 > 255)
					{
						num34 = 255;
					}
					if (num35 < 0)
					{
						num35 = 0;
					}
					if (num35 > 255)
					{
						num35 = 255;
					}
					if (num36 < 0)
					{
						num36 = 0;
					}
					if (num36 > 255)
					{
						num36 = 255;
					}
					int num37 = num36 + 1;
					pixels[i * num2 + j] = (num36 << 24) | ((byte)(num33 * num37 >> 8) << 16) | ((byte)(num34 * num37 >> 8) << 8) | (byte)(num35 * num37 >> 8);
				}
			}
		}
		return writeableBitmap;
	}

	public unsafe static WriteableBitmap Flip(this WriteableBitmap bmp, FlipMode flipMode)
	{
		using BitmapContext bitmapContext = bmp.GetBitmapContext(ReadWriteMode.ReadOnly);
		int width = bitmapContext.Width;
		int height = bitmapContext.Height;
		int* pixels = bitmapContext.Pixels;
		int num = 0;
		WriteableBitmap writeableBitmap = null;
		switch (flipMode)
		{
		case FlipMode.Horizontal:
		{
			writeableBitmap = BitmapFactory.New(width, height);
			using (BitmapContext bitmapContext3 = writeableBitmap.GetBitmapContext())
			{
				int* pixels3 = bitmapContext3.Pixels;
				for (int num4 = height - 1; num4 >= 0; num4--)
				{
					for (int j = 0; j < width; j++)
					{
						int num5 = num4 * width + j;
						pixels3[num] = pixels[num5];
						num++;
					}
				}
			}
			break;
		}
		case FlipMode.Vertical:
		{
			writeableBitmap = BitmapFactory.New(width, height);
			using (BitmapContext bitmapContext2 = writeableBitmap.GetBitmapContext())
			{
				int* pixels2 = bitmapContext2.Pixels;
				for (int i = 0; i < height; i++)
				{
					for (int num2 = width - 1; num2 >= 0; num2--)
					{
						int num3 = i * width + num2;
						pixels2[num] = pixels[num3];
						num++;
					}
				}
			}
			break;
		}
		}
		return writeableBitmap;
	}
}
