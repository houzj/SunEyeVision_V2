using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media.Imaging;
using OpenCvSharp;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public class CvImage : IVisionImage, IMonitorable, IDisposable
    {
        internal List<ImageItem> _items;

        private CvImage()
        {
            _items = new List<ImageItem>();
        }

        public CvImage(Mat image, bool ownsImage = false)
        {
            _items = new List<ImageItem>();
            if (image != null && !image.Empty())
                _items.Add(new ImageItem(image, ownsImage));
        }

        internal CvImage(List<ImageItem> items)
        {
            _items = items ?? new List<ImageItem>();
        }

        public static CvImage FromMat(Mat image) => new(image.Clone(), ownsImage: true);

        public static CvImage Create(Size size, Scalar color, int channels = 1)
        {
            var type = channels == 1 ? MatType.CV_8UC1 : MatType.CV_8UC3;
            return new CvImage(new Mat(size, type, color), ownsImage: true);
        }

        public static CvImage GenEmptyObj() => new();

        public int Count => _items.Count;

        public Mat Image => _items.Count > 0
            ? _items[0].Image
            : throw new InvalidOperationException("Empty image object");

        public Size Size => Image.Size();

        public int Channels => Image.Channels();

        public bool Empty => _items.Count == 0;

        int IVisionImage.Width => Size.Width;
        int IVisionImage.Height => Size.Height;
        bool IVisionImage.IsEmpty => Empty;

        BitmapSource IVisionImage.ToBitmapSource()
        {
            var src = Image;
            var pixelFormat = src.Channels() switch
            {
                1 => System.Windows.Media.PixelFormats.Gray8,
                3 => System.Windows.Media.PixelFormats.Bgr24,
                4 => System.Windows.Media.PixelFormats.Bgra32,
                _ => System.Windows.Media.PixelFormats.Gray8
            };
            int stride = src.Width * src.Channels();
            var bytes = new byte[stride * src.Height];
            System.Runtime.InteropServices.Marshal.Copy(src.Data, bytes, 0, bytes.Length);
            return BitmapSource.Create(src.Width, src.Height, 96, 96, pixelFormat, null, bytes, stride);
        }

        byte[] IVisionImage.GetPixelData()
        {
            var src = Image;
            var bytes = new byte[src.Total() * src.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(src.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        IVisionImage IVisionImage.Clone() => CopyObj();

        public CvImage CopyObj()
        {
            var items = new List<ImageItem>(_items.Count);
            foreach (var item in _items)
            {
                if (item.Image != null && !item.Image.Empty())
                    items.Add(new ImageItem(item.Image.Clone(), ownsImage: true));
            }
            return new CvImage(items);
        }

        public void Dispose()
        {
            foreach (var item in _items) item.Dispose();
            _items.Clear();
            GC.SuppressFinalize(this);
        }

        ~CvImage()
        {
            foreach (var item in _items) item.Dispose();
        }

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            int w = 0, h = 0, ch = 0;
            if (_items.Count > 0)
            {
                var src = _items[0].Image;
                if (src != null && !src.Empty())
                {
                    w = src.Width;
                    h = src.Height;
                    ch = src.Channels();
                }
            }
            return new (string, object?)[]
            {
                ("宽度", w),
                ("高度", h),
                ("通道数", ch),
            };
        }
    }

    internal class ImageItem : IDisposable
    {
        private readonly Mat _image;
        private readonly bool _ownsImage;

        internal ImageItem(Mat image, bool ownsImage = true)
        {
            _image = image;
            _ownsImage = ownsImage;
        }

        internal Mat Image => _image;

        public void Dispose()
        {
            if (_ownsImage && _image != null) _image.Dispose();
        }
    }
}