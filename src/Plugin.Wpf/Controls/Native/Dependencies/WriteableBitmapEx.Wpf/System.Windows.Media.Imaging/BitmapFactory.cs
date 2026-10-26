using System;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;
using System.IO;
using System.Reflection;

namespace SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;

public static class BitmapFactory
{
	public static WriteableBitmap New(int pixelWidth, int pixelHeight)
	{
		if (pixelHeight < 1)
		{
			pixelHeight = 1;
		}
		if (pixelWidth < 1)
		{
			pixelWidth = 1;
		}
		return new WriteableBitmap(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32, null);
	}

	public static WriteableBitmap ConvertToPbgra32Format(BitmapSource source)
	{
		if (source.Format == PixelFormats.Pbgra32)
		{
			return new WriteableBitmap(source);
		}
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap();
		formatConvertedBitmap.BeginInit();
		formatConvertedBitmap.Source = source;
		formatConvertedBitmap.DestinationFormat = PixelFormats.Pbgra32;
		formatConvertedBitmap.EndInit();
		return new WriteableBitmap(formatConvertedBitmap);
	}

	public static WriteableBitmap FromResource(string relativePath)
	{
		return FromContent(new AssemblyName(Assembly.GetCallingAssembly().FullName).Name + ";component/" + relativePath);
	}

	public static WriteableBitmap FromContent(string relativePath)
	{
		using Stream stream = Application.GetResourceStream(new Uri(relativePath, UriKind.Relative)).Stream;
		return FromStream(stream);
	}

	public static WriteableBitmap FromStream(Stream stream)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CreateOptions = BitmapCreateOptions.None;
		bitmapImage.StreamSource = stream;
		bitmapImage.EndInit();
		WriteableBitmap result = new WriteableBitmap(bitmapImage);
		bitmapImage.UriSource = null;
		return result;
	}
}
