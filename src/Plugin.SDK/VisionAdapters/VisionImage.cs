using System;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public static class VisionImage
    {
        public static IVisionImage FromFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return HalImage.CreateFromFile(path);
        }

        public static IVisionImage FromBytes(byte[] data, int width, int height, int channels)
        {
            ArgumentNullException.ThrowIfNull(data);
            return HalImage.CreateFromBytes(data, width, height, channels);
        }

        public static IVisionImage FromBitmapSource(BitmapSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return HalImage.FromBitmapSource(source);
        }

        public static IVisionImage FromMat(Mat image)
        {
            ArgumentNullException.ThrowIfNull(image);
            return CvImage.FromMat(image);
        }
    }
}