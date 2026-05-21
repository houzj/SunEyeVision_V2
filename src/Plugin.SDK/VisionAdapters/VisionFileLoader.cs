using System;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    internal static class VisionFileLoader
    {
        public static BitmapSource LoadFromFile(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
    }
}