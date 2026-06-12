using System;
using System.Linq;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public class CvContour : IVisionContour, IDisposable
    {
        private Point[]? _points;
        private bool _isClosed;
        private Size _imageSize;

        public Point[]? Points => _points;
        public bool IsClosed => _isClosed;
        public Size ImageSize => _imageSize;
        public bool IsEmpty => _points == null || _points.Length == 0;

        WpfPoint[] IVisionContour.Points
        {
            get
            {
                if (_points == null) return Array.Empty<WpfPoint>();
                return _points.Select(p => new WpfPoint(p.X, p.Y)).ToArray();
            }
        }

        IVisionContour IVisionContour.Clone() => CopyObj();

        private CvContour()
        {
            _points = null;
            _isClosed = false;
            _imageSize = new Size(0, 0);
        }

        public static CvContour GenEmptyObj() => new();

        public static CvContour Create(Point[] points, bool isClosed, Size imageSize)
        {
            return new CvContour { _points = points, _isClosed = isClosed, _imageSize = imageSize };
        }

        public CvContour CopyObj()
        {
            return new CvContour { _points = _points?.ToArray(), _isClosed = _isClosed, _imageSize = _imageSize };
        }

        public void Dispose()
        {
            _points = null;
            GC.SuppressFinalize(this);
        }
    }
}