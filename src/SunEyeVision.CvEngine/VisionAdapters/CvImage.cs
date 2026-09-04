using System;
using System.Collections.Generic;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// CvEngine 图像载体（算法层纯类型）：包装 OpenCvSharp.Mat 的图像容器，支持多图（ImageItem 列表）。
    /// 对齐 Halcon HImage 的角色，但只承载算法数据，不含任何框架契约职责
    /// （IVisionImage / IMonitorable / WPF BitmapSource 转换由 CvAdapter 适配提供）。
    /// 生命周期由调用方管理，使用完毕必须调用 <see cref="Dispose"/>。
    /// </summary>
    public class CvImage : IDisposable
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
    }

    /// <summary>单张 Mat 的持有项，负责 ownsImage 语义下的释放。</summary>
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
