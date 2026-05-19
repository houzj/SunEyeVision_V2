using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Imaging;

namespace SunEyeVision.HalAdapter
{
    /// <summary>
    /// Halcon HImage 的适配包装，实现 <see cref="IVisionImage"/>
    /// </summary>
    public sealed class HalImage : IVisionImage
    {
        private readonly HImage _image;
        private bool _disposed;

        public HalImage(HImage image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        /// <summary>
        /// 拆包获取内部 HImage 引用（不转移所有权）
        /// </summary>
        public HImage Unwrap()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _image;
        }

        public int Width
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                HOperatorSet.GetImageSize(_image, out var w, out _);
                return (int)w.L;
            }
        }

        public int Height
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                HOperatorSet.GetImageSize(_image, out _, out var h);
                return (int)h.L;
            }
        }

        public int Channels
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _image.CountChannels();
            }
        }

        public bool IsEmpty
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return !_image.IsInitialized();
            }
        }

        public BitmapSource ToBitmapSource()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            int w = Width;
            int h = Height;

            if (Channels == 1)
            {
                HOperatorSet.GetImagePointer1(_image, out var ptr, out _, out _, out _);
                return BitmapSource.Create(
                    w, h, 96, 96,
                    PixelFormats.Gray8, null,
                    ptr, w * h, w);
            }

            // 多通道：直接 GetImagePointer3 → BGR 交错
            HOperatorSet.GetImagePointer3(_image, out var ptrR, out var ptrG, out var ptrB,
                out _, out _, out _);

            int stride = w * 3;
            var pixels = new byte[stride * h];
            var rowR = new byte[w];
            var rowG = new byte[w];
            var rowB = new byte[w];

            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(ptrR + y * w, rowR, 0, w);
                Marshal.Copy(ptrG + y * w, rowG, 0, w);
                Marshal.Copy(ptrB + y * w, rowB, 0, w);

                int rowOffset = y * stride;
                for (int x = 0; x < w; x++)
                {
                    pixels[rowOffset + x * 3] = rowB[x];      // B
                    pixels[rowOffset + x * 3 + 1] = rowG[x];  // G
                    pixels[rowOffset + x * 3 + 2] = rowR[x];  // R
                }
            }

            return BitmapSource.Create(w, h, 96, 96,
                PixelFormats.Bgr24, null,
                pixels, stride);
        }

        public byte[] GetPixelData()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (Channels == 1)
            {
                HOperatorSet.GetImagePointer1(_image, out var ptr, out _, out var imgW, out var imgH);
                var bytes = new byte[(int)imgW.L * (int)imgH.L];
                Marshal.Copy(ptr, bytes, 0, bytes.Length);
                return bytes;
            }

            // 多通道：R/G/B → RGB 交错
            HOperatorSet.GetImagePointer3(_image, out var ptrR, out var ptrG, out var ptrB,
                out _, out var w3, out var h3);

            int w = (int)w3.L;
            int h = (int)h3.L;
            var result = new byte[w * h * 3];
            var rowR = new byte[w];
            var rowG = new byte[w];
            var rowB = new byte[w];

            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(ptrR + y * w, rowR, 0, w);
                Marshal.Copy(ptrG + y * w, rowG, 0, w);
                Marshal.Copy(ptrB + y * w, rowB, 0, w);

                int rowOffset = y * w * 3;
                for (int x = 0; x < w; x++)
                {
                    result[rowOffset + x * 3] = rowR[x];
                    result[rowOffset + x * 3 + 1] = rowG[x];
                    result[rowOffset + x * 3 + 2] = rowB[x];
                }
            }

            return result;
        }

        public IVisionImage Clone()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new HalImage(_image.Clone());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _image.Dispose();
            }
        }
    }
}
