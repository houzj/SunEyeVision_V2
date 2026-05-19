using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Imaging;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// Halcon 引擎的 <see cref="IVisionFactory"/> 实现
    /// </summary>
    public sealed class HalVisionFactory : IVisionFactory
    {
        public IVisionImage LoadFromFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return new HalImage(new HImage(path));
        }

        public IVisionImage LoadFromBitmapSource(BitmapSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            int pw = source.PixelWidth;
            int ph = source.PixelHeight;
            var data = new byte[pw * ph * 4];
            source.CopyPixels(data, pw * 4, 0);

            // BGRA → gray (取 R 通道)
            var grayData = new byte[pw * ph];
            for (int i = 0; i < pw * ph; i++)
                grayData[i] = data[i * 4 + 2];

            unsafe
            {
                fixed (byte* p = grayData)
                {
                    return new HalImage(new HImage("byte", pw, ph, (IntPtr)p));
                }
            }
        }

        public IVisionImage LoadFromBytes(byte[] data, int width, int height, int channels)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (channels is not (1 or 3)) throw new NotSupportedException($"不支持的通道数: {channels}");

            HImage hImage;
            unsafe
            {
                fixed (byte* p = data)
                {
                    if (channels == 1)
                    {
                        hImage = new HImage("byte", width, height, (IntPtr)p);
                    }
                    else
                    {
                        hImage = new HImage();
                        hImage.GenImageInterleaved(
                            (IntPtr)p, "rgb", width, height, 0,
                            "byte", 0, 0, 0, 0, -1, 0);
                    }
                }
            }

            return new HalImage(hImage);
        }

        public IVisionRegion BuildRectangle(double row1, double col1, double row2, double col2)
        {
            return new HalRegion(new HRegion(row1, col1, row2, col2));
        }

        public IVisionRegion BuildCircle(double row, double col, double radius)
        {
            return new HalRegion(new HRegion(row, col, radius));
        }

        public IVisionRegion BuildPolygon(Point[] points)
        {
            ArgumentNullException.ThrowIfNull(points);
            if (points.Length < 3) throw new ArgumentException("多边形至少需要3个点", nameof(points));

            int n = points.Length;
            var rows = new HTuple(n);
            var cols = new HTuple(n);
            for (int i = 0; i < n; i++)
            {
                rows[i] = points[i].Y;
                cols[i] = points[i].X;
            }

            var region = new HRegion();
            region.GenRegionPolygon(rows, cols);
            return new HalRegion(region);
        }

        public IVisionRegion CreateRegionFromMask(byte[] maskData, int width, int height)
        {
            unsafe
            {
                fixed (byte* p = maskData)
                {
                    using var maskImage = new HImage("byte", width, height, (IntPtr)p);
                    var region = maskImage.Threshold(1.0, 255.0);
                    return new HalRegion(region);
                }
            }
        }

        public IVisionRegion Union(IVisionRegion a, IVisionRegion b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            var ha = CastAndUnwrap(a);
            var hb = CastAndUnwrap(b);
            HOperatorSet.Union2(ha, hb, out HObject result);
            return new HalRegion(new HRegion(result));
        }

        public IVisionRegion Difference(IVisionRegion a, IVisionRegion b)
        {
            ArgumentNullException.ThrowIfNull(a);
            ArgumentNullException.ThrowIfNull(b);
            var ha = CastAndUnwrap(a);
            var hb = CastAndUnwrap(b);
            return new HalRegion(ha.Difference(hb));
        }

        private static HRegion CastAndUnwrap(IVisionRegion region)
        {
            return region is HalRegion halRegion
                ? halRegion.Unwrap()
                : throw new ArgumentException($"区域类型不匹配：期望 {nameof(HalRegion)}，实际为 {region.GetType().Name}");
        }
    }
}
