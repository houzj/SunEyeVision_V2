using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;

namespace SunEyeVision.UI.Services.Performance
{
    public class ImageConverterPool : IDisposable
    {
        private readonly ConcurrentDictionary<string, WriteableBitmap> _bitmapCache;
        private readonly int _maxCacheSize;
        private bool _disposed;

        public ImageConverterPool(int maxCacheSize = 10)
        {
            _bitmapCache = new ConcurrentDictionary<string, WriteableBitmap>();
            _maxCacheSize = maxCacheSize;
        }

        public BitmapSource ConvertToBitmapSource(HImage image)
        {
            if (image == null || !image.IsInitialized())
                throw new ArgumentNullException(nameof(image));

            var info = GetImageInfo(image);
            PixelFormat pixelFormat = GetPixelFormat(info.C);

            string cacheKey = $"{info.W}_{info.H}_{pixelFormat}";

            if (!_bitmapCache.TryGetValue(cacheKey, out var writeableBitmap) ||
                writeableBitmap.PixelWidth != info.W ||
                writeableBitmap.PixelHeight != info.H)
            {
                writeableBitmap = new WriteableBitmap(info.W, info.H, 96, 96, pixelFormat, null);

                if (_bitmapCache.Count < _maxCacheSize)
                {
                    _bitmapCache.TryAdd(cacheKey, writeableBitmap);
                }
            }

                writeableBitmap.Lock();

            try
            {
                if (info.C == 1)
                {
                    CopyGrayToBitmap(image, writeableBitmap, info);
                }
                else if (info.C == 3)
                {
                    CopyRgbToBitmap(image, writeableBitmap, info);
                }
                else if (info.C == 4)
                {
                    CopyRgbaToBitmap(image, writeableBitmap, info);
                }
                else
                {
                    throw new NotSupportedException($"Unsupported channel count: {info.C}");
                }

                writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, info.W, info.H));
            }
            finally
            {
                writeableBitmap.Unlock();
            }

            return writeableBitmap;
        }

        private static (int W, int H, int C, string T) GetImageInfo(HImage image)
        {
            HOperatorSet.GetImageSize(image, out var w, out var h);
            HOperatorSet.CountChannels(image, out var c);
            HOperatorSet.GetImageType(image, out var t);
            return (w.I, h.I, c.I, t.S);
        }

        private static PixelFormat GetPixelFormat(int channels)
        {
            return channels switch
            {
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Rgb24,
                4 => PixelFormats.Bgra32,
                _ => PixelFormats.Bgr24
            };
        }

        private static void CopyGrayToBitmap(HImage image, WriteableBitmap bitmap, (int W, int H, int C, string T) info)
        {
            HOperatorSet.GetImagePointer1(image, out var pointer, out var type, out var width, out var height);
            int stride = info.W;
            int bpp = (bitmap.Format.BitsPerPixel + 7) / 8;
            int dstStride = info.W * bpp;

            unsafe
            {
                byte* src = (byte*)pointer.IP;
                byte* dst = (byte*)bitmap.BackBuffer;

                for (int y = 0; y < info.H; y++)
                {
                    Buffer.MemoryCopy(src + y * stride, dst + y * dstStride, dstStride, stride);
                }
            }
        }

        private static unsafe void CopyRgbToBitmap(HImage image, WriteableBitmap bitmap, (int W, int H, int C, string T) info)
        {
            HOperatorSet.GetImagePointer3(image, out var ptrR, out var ptrG, out var ptrB,
                out var type, out var width, out var height);

            byte* rPtr = (byte*)ptrR.IP;
            byte* gPtr = (byte*)ptrG.IP;
            byte* bPtr = (byte*)ptrB.IP;
            byte* dst = (byte*)bitmap.BackBuffer;
            int dstStride = info.W * 3;

            for (int y = 0; y < info.H; y++)
            {
                for (int x = 0; x < info.W; x++)
                {
                    int idx = y * dstStride + x * 3;
                    dst[idx] = rPtr[y * info.W + x];
                    dst[idx + 1] = gPtr[y * info.W + x];
                    dst[idx + 2] = bPtr[y * info.W + x];
                }
            }
        }

        private static unsafe void CopyRgbaToBitmap(HImage image, WriteableBitmap bitmap, (int W, int H, int C, string T) info)
        {
            HOperatorSet.GetImagePointer3(image, out var ptrR, out var ptrG, out var ptrB,
                out var type, out var width, out var height);

            byte* rPtr = (byte*)ptrR.IP;
            byte* gPtr = (byte*)ptrG.IP;
            byte* bPtr = (byte*)ptrB.IP;
            byte* dst = (byte*)bitmap.BackBuffer;
            int dstStride = info.W * 4;

            for (int y = 0; y < info.H; y++)
            {
                for (int x = 0; x < info.W; x++)
                {
                    int idx = y * dstStride + x * 4;
                    dst[idx] = bPtr[y * info.W + x];
                    dst[idx + 1] = gPtr[y * info.W + x];
                    dst[idx + 2] = rPtr[y * info.W + x];
                    dst[idx + 3] = 255;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _bitmapCache.Clear();
                _disposed = true;
            }
        }
    }
}
