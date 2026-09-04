using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using CvImage = SunEyeVision.CvEngine.CvImage;

namespace SunEyeVision.CvAdapter
{
    /// <summary>
    /// Cv 图像适配器：把 CvEngine 纯图像（<see cref="CvImage"/>，Mat 载体）桥接为 SDK 契约
    /// <see cref="IVisionImage"/>。CvEngine 作为纯算法库不实现 SDK 接口（对称架构），
    /// 本类在适配层完成包装；生命周期由调用方管理，Dispose 转交被包装的 CvImage。
    /// </summary>
    public sealed class CvVisionImageAdapter : IVisionImage
    {
        private readonly CvImage _image;
        private bool _disposed;

        public CvVisionImageAdapter(CvImage image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public CvImage Unwrap() => _image;

        public int Width
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _image.Size.Width;
            }
        }

        public int Height
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _image.Size.Height;
            }
        }

        public int Channels
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _image.Channels;
            }
        }

        public bool IsEmpty
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _image.Empty;
            }
        }

        public byte[] GetPixelData()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var src = _image.Image;
            var bytes = new byte[src.Total() * src.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(src.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        /// <summary>转换为 WPF BitmapSource（显示桥 UI 适配层使用）。</summary>
        public BitmapSource ToBitmapSource()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var src = _image.Image;
            var pixelFormat = src.Channels() switch
            {
                1 => PixelFormats.Gray8,
                3 => PixelFormats.Bgr24,
                4 => PixelFormats.Bgra32,
                _ => PixelFormats.Gray8
            };
            int stride = src.Width * src.Channels();
            var bytes = new byte[stride * src.Height];
            System.Runtime.InteropServices.Marshal.Copy(src.Data, bytes, 0, bytes.Length);
            return BitmapSource.Create(src.Width, src.Height, 96, 96, pixelFormat, null, bytes, stride);
        }

        public IVisionImage Clone()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new CvVisionImageAdapter(_image.CopyObj());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _image.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
