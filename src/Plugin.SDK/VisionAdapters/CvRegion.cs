using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using WpfRect = System.Windows.Rect;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public class CvRegion : IVisionRegion, IDisposable
    {
        internal List<RegionItem> _items;
        internal Size _imageSize;
        internal Mat? _ccLabels;
        internal Mat? _ccStats;
        internal Mat? _ccCentroids;
        private Mat? _combinedMaskCache;

        private CvRegion()
        {
            _items = new List<RegionItem>();
            _imageSize = new Size(0, 0);
        }

        internal CvRegion(Mat? mask, Size imageSize)
        {
            _items = new List<RegionItem>();
            _imageSize = imageSize;
            if (mask != null && !mask.Empty() && Cv2.CountNonZero(mask) > 0)
                _items.Add(new RegionItem(mask));
        }

        internal CvRegion(List<RegionItem> items, Size imageSize,
            Mat? ccLabels = null, Mat? ccStats = null, Mat? ccCentroids = null)
        {
            _items = items ?? new List<RegionItem>();
            _imageSize = imageSize;
            _ccLabels = ccLabels;
            _ccStats = ccStats;
            _ccCentroids = ccCentroids;
        }

        public static CvRegion GenEmptyObj() => new();

        public static CvRegion GenRectangle1(int row1, int col1, int row2, int col2)
        {
            int x = Math.Min(col1, col2);
            int y = Math.Min(row1, row2);
            int w = Math.Abs(col2 - col1) + 1;
            int h = Math.Abs(row2 - row1) + 1;
            var imageSize = new Size(x + w, y + h);
            var mask = new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);
            Cv2.Rectangle(mask, new Rect(x, y, w, h), Scalar.White, -1);
            return new CvRegion(mask, imageSize);
        }

        public static CvRegion FromMask(Mat mask, bool ownsData = false)
        {
            if (mask == null || mask.Empty())
                return GenEmptyObj();
            var m = ownsData ? mask : mask.Clone();
            return new CvRegion(m, mask.Size());
        }

        /// <summary>
        /// From Regions from RegionData collection
        /// </summary>
        public static CvRegion CreateFromRegions(Size imageSize, IEnumerable<RegionData> regions)
        {
            var enabled = regions.Where(r => r.IsEnabled).ToList();
            if (enabled.Count == 0)
                return GenEmptyObj();

            var combinedMask = new Mat(imageSize, MatType.CV_8UC1, Scalar.Black);

            foreach (var region in enabled)
            {
                if (region.Parameters is not ShapeParameters shape) continue;

                switch (shape.ShapeType)
                {
                    case ShapeType.Rectangle:
                    case ShapeType.RotatedRectangle:
                    {
                        int x = (int)Math.Round(shape.CenterX - shape.Width / 2);
                        int y = (int)Math.Round(shape.CenterY - shape.Height / 2);
                        int w = (int)Math.Round(shape.Width);
                        int h = (int)Math.Round(shape.Height);
                        Cv2.Rectangle(combinedMask, new Rect(x, y, w, h), Scalar.White, -1);
                        break;
                    }
                    case ShapeType.Circle:
                    {
                        int cx = (int)Math.Round(shape.CenterX);
                        int cy = (int)Math.Round(shape.CenterY);
                        int r = (int)Math.Round(shape.Radius);
                        Cv2.Circle(combinedMask, cx, cy, r, Scalar.White, -1);
                        break;
                    }
                    case ShapeType.Polygon when shape.Points.Count >= 3:
                    {
                        var pts = shape.Points.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
                        Cv2.FillPoly(combinedMask, new[] { pts }, Scalar.White);
                        break;
                    }
                }
            }

            return new CvRegion(combinedMask, imageSize);
        }

        public int Count => _items.Count;
        public Size ImageSize => _imageSize;
        public bool IsEmpty => _items.Count == 0;

        public int PixelCount
        {
            get
            {
                var combined = GetCombinedMask();
                return combined != null && !combined.Empty() ? Cv2.CountNonZero(combined) : 0;
            }
        }

        WpfRect IVisionRegion.BoundingBox
        {
            get
            {
                var mask = GetCombinedMask();
                if (mask == null || mask.Empty())
                    return WpfRect.Empty;
                var rect = Cv2.BoundingRect(mask);
                return new WpfRect(rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        WpfPoint[] IVisionRegion.GetContour()
        {
            var mask = GetCombinedMask();
            if (mask == null || mask.Empty())
                return Array.Empty<WpfPoint>();
            var contours = Cv2.FindContoursAsArray(mask, RetrievalModes.External, ContourApproximationModes.ApproxNone);
            if (contours.Length == 0)
                return Array.Empty<WpfPoint>();
            return contours[0].Select(p => new WpfPoint(p.X, p.Y)).ToArray();
        }

        byte[] IVisionRegion.GetMaskData()
        {
            var mask = GetCombinedMask();
            if (mask == null || mask.Empty())
                return Array.Empty<byte>();
            var bytes = new byte[mask.Total() * mask.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(mask.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        IVisionRegion IVisionRegion.Clone() => CopyObj();

        internal Mat? Mask => GetCombinedMask();
        internal bool HasLabels => _ccLabels != null && !_ccLabels.Empty();
        internal Mat? Labels => _ccLabels;
        internal Mat? Stats => _ccStats;
        internal Mat? Centroids => _ccCentroids;

        internal Mat? GetCombinedMask()
        {
            if (_combinedMaskCache != null) return _combinedMaskCache;
            if (_items.Count == 0) return null;
            if (_ccLabels != null && !_ccLabels.Empty())
            {
                _combinedMaskCache = new Mat();
                Cv2.Compare(_ccLabels, 0, _combinedMaskCache, CmpType.GT);
                return _combinedMaskCache;
            }
            _combinedMaskCache = new Mat(_imageSize, MatType.CV_8UC1, Scalar.Black);
            foreach (var item in _items)
            {
                var mask = item.GetMask();
                if (mask != null && !mask.Empty())
                    Cv2.BitwiseOr(_combinedMaskCache, mask, _combinedMaskCache);
            }
            return _combinedMaskCache;
        }

        public CvRegion CopyObj()
        {
            foreach (var item in _items)
                item.GetMask();
            var items = new List<RegionItem>(_items.Count);
            foreach (var item in _items)
            {
                var mask = item.Mask;
                if (mask != null && !mask.Empty())
                    items.Add(new RegionItem(mask.Clone()));
            }
            return new CvRegion(items, _imageSize,
                _ccLabels?.Clone(), _ccStats?.Clone(), _ccCentroids?.Clone());
        }

        public void Dispose()
        {
            foreach (var item in _items) item.Dispose();
            _items.Clear();
            _combinedMaskCache?.Dispose(); _combinedMaskCache = null;
            _ccLabels?.Dispose(); _ccLabels = null;
            _ccStats?.Dispose(); _ccStats = null;
            _ccCentroids?.Dispose(); _ccCentroids = null;
            GC.SuppressFinalize(this);
        }

        ~CvRegion()
        {
            foreach (var item in _items) item.Dispose();
            _combinedMaskCache?.Dispose();
            _ccLabels?.Dispose();
            _ccStats?.Dispose();
            _ccCentroids?.Dispose();
        }
    }

    internal class RegionItem : IDisposable
    {
        private Mat? _mask;
        private readonly Mat? _ccLabelsRef;
        private readonly int _labelIndex;
        private readonly Size _imageSize;

        internal RegionItem(Mat mask) { _mask = mask; }

        internal RegionItem(Mat ccLabelsRef, int labelIndex, Size imageSize)
        {
            _ccLabelsRef = ccLabelsRef;
            _labelIndex = labelIndex;
            _imageSize = imageSize;
        }

        internal Mat? Mask => _mask;

        internal Mat? GetMask()
        {
            if (_mask != null) return _mask;
            if (_ccLabelsRef == null || _ccLabelsRef.Empty()) return null;
            _mask = new Mat();
            Cv2.Compare(_ccLabelsRef, _labelIndex, _mask, CmpType.EQ);
            return _mask;
        }

        public void Dispose()
        {
            _mask?.Dispose();
            _mask = null;
        }
    }
}