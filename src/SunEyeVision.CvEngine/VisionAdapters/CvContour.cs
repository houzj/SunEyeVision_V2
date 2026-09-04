using System;
using System.Linq;
using OpenCvSharp;

namespace SunEyeVision.CvEngine
{
    /// <summary>
    /// CvEngine 轮廓载体（算法层纯类型）：以 OpenCvSharp.Point 序列表示轮廓。
    /// 对齐 Halcon XLD 轮廓的角色，但只承载算法数据，不含任何框架契约职责
    /// （IVisionContour 由 CvAdapter 适配提供）。
    /// </summary>
    public class CvContour : IDisposable
    {
        private Point[]? _points;
        private bool _isClosed;
        private Size _imageSize;

        public Point[]? Points => _points;

        public bool IsClosed => _isClosed;

        public Size ImageSize => _imageSize;

        public bool IsEmpty => _points == null || _points.Length == 0;

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
