using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;

public static class WriteableBitmapContextExtensions
{
	public static BitmapContext GetBitmapContext(this WriteableBitmap bmp)
	{
		return new BitmapContext(bmp);
	}

	public static BitmapContext GetBitmapContext(this WriteableBitmap bmp, ReadWriteMode mode)
	{
		return new BitmapContext(bmp, mode);
	}
}
