using System.IO;
using System.Windows.Media.Imaging;

namespace IMVSColorRecognitionModuControl;

public static class ImageHelpers
{
	public static BitmapImage ToBitmapImage(this WriteableBitmap wbm, int width, int height)
	{
		BitmapImage bitmapImage = new BitmapImage();
		using (MemoryStream memoryStream = new MemoryStream())
		{
			PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
			pngBitmapEncoder.Frames.Add(BitmapFrame.Create(wbm));
			pngBitmapEncoder.Save(memoryStream);
			bitmapImage.BeginInit();
			bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
			bitmapImage.StreamSource = memoryStream;
			bitmapImage.DecodePixelHeight = height;
			bitmapImage.DecodePixelWidth = width;
			bitmapImage.EndInit();
			bitmapImage.Freeze();
		}
		return bitmapImage;
	}
}
