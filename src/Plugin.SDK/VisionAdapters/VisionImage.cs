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

            // S1 解耦接缝：优先经注册工厂加载（算法库/适配层注入），
            // 未注册回退 SDK 内默认 Halcon 路径（过渡期行为，S4 摘除引擎后由工厂独占）。
            var factory = VisionImageFactoryLocator.Current;
            if (factory != null && factory.CanCreateFromFile(path))
                return factory.CreateFromFile(path);

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