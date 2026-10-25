using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.HalAdapter
{
    public sealed class HalImage : IVisionImage, IMonitorable, ITypedImageSnapshotSource
    {
        private readonly HImage _image;
        private bool _disposed;
        private readonly object _snapshotGate = new();

        public SunEyeVision.Plugin.Runtime.FrozenImageResource CaptureSnapshot(SunEyeVision.Plugin.Contracts.ImageFrameAddress frame)
        {
            HImage captured;
            lock(_snapshotGate)
            {
                ObjectDisposedException.ThrowIf(_disposed,this);
                captured=_image.CopyImage();
            }
            using(captured)return HalImageSnapshotSource.Capture(captured,frame);
        }

        public HalImage(HImage image)
        {
            _image = image ?? throw new ArgumentNullException(nameof(image));
        }

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
                return BitmapSource.Create(w, h, 96, 96, PixelFormats.Gray8, null, ptr, w * h, w);
            }
            HOperatorSet.GetImagePointer3(_image, out var ptrR, out var ptrG, out var ptrB, out _, out _, out _);
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
                    pixels[rowOffset + x * 3] = rowB[x];
                    pixels[rowOffset + x * 3 + 1] = rowG[x];
                    pixels[rowOffset + x * 3 + 2] = rowR[x];
                }
            }
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, pixels, stride);
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
            HOperatorSet.GetImagePointer3(_image, out var ptrR, out var ptrG, out var ptrB, out _, out var w3, out var h3);
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
            lock(_snapshotGate)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    _image.Dispose();
                }
            }
        }

        #region Static factory methods

        public static HalImage CreateFromFile(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            return new HalImage(new HImage(path));
        }

        public static HalImage FromBitmapSource(BitmapSource source)
        {
            ArgumentNullException.ThrowIfNull(source);
            int w = source.PixelWidth;
            int h = source.PixelHeight;
            int bpp = source.Format.BitsPerPixel;
            var pixelData = new byte[w * h * (bpp / 8)];
            source.CopyPixels(pixelData, w * (bpp / 8), 0);
            unsafe
            {
                fixed (byte* p = pixelData)
                {
                    if (bpp == 8)
                        return new HalImage(new HImage("byte", w, h, (IntPtr)p));
                    string colorFormat = bpp switch { 24 => "bgr", 32 => "bgrx", _ => "bgr" };
                    var hImg = new HImage();
                    hImg.GenImageInterleaved((IntPtr)p, colorFormat, w, h, 0, "byte", 0, 0, 0, 0, -1, 0);
                    return new HalImage(hImg);
                }
            }
        }

        public static HalImage CreateFromBytes(byte[] data, int width, int height, int channels)
        {
            ArgumentNullException.ThrowIfNull(data);
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
            if (channels is not (1 or 3)) throw new NotSupportedException($"channels: {channels}");

            HalImage halImage;
            unsafe
            {
                fixed (byte* p = data)
                {
                    if (channels == 1)
                        halImage = new HalImage(new HImage("byte", width, height, (IntPtr)p));
                    else
                    {
                        var hImg = new HImage();
                        hImg.GenImageInterleaved((IntPtr)p, "rgb", width, height, 0, "byte", 0, 0, 0, 0, -1, 0);
                        halImage = new HalImage(hImg);
                    }
                }
            }
            return halImage;
        }

        public static HalImage CreateGrayFromColor(IVisionImage colorImage)
        {
            ArgumentNullException.ThrowIfNull(colorImage);
            var halImage = colorImage as HalImage ?? throw new ArgumentException($"Expected {nameof(HalImage)}, got {colorImage.GetType().Name}");
            lock(halImage._snapshotGate)
            {
                ObjectDisposedException.ThrowIf(halImage._disposed,halImage);
                return new HalImage(CreateGrayNative(halImage._image));
            }
        }

        /// <summary>Creates an owned gray image, preserving the actual source Domain. Alpha is not luminance.</summary>
        public static HImage CreateGrayNative(HImage source)
        {
            ArgumentNullException.ThrowIfNull(source);
            var channels=source.CountChannels();
            if(channels==1)return source.CopyImage();
            if(channels==3)return source.Rgb1ToGray();
            if(channels!=4)throw new NotSupportedException($"Gray conversion requires 1, 3 or 4 channels; actual {channels}.");
            using var red=source.AccessChannel(1);
            using var green=source.AccessChannel(2);
            using var blue=source.AccessChannel(3);
            using var rgb=red.Compose3(green,blue);
            return rgb.Rgb1ToGray();
        }

        #endregion

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("宽度", Width),
                ("高度", Height),
                ("通道数", Channels),
            };
        }
    }
}
