using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using HalconDotNet;

namespace SunEyeVision.Plugin.SDK.VisionAdapters
{
    public sealed class HalRegion : IVisionRegion, IMonitorable
    {
        private readonly HRegion _region;
        private bool _disposed;

        public HalRegion(HRegion region)
        {
            _region = region ?? throw new ArgumentNullException(nameof(region));
        }

        public HRegion Unwrap()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _region;
        }

        public int PixelCount
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _region.Area;
            }
        }

        public Rect BoundingBox
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                HOperatorSet.SmallestRectangle1(_region, out var r1, out var c1, out var r2, out var c2);
                return new Rect(c1.L, r1.L, c2.L - c1.L + 1, r2.L - r1.L + 1);
            }
        }

        public bool IsEmpty
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return !_region.IsInitialized();
            }
        }

        public Point[] GetContour()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            HOperatorSet.GetRegionContour(_region, out var rows, out var cols);
            if (rows == null || cols == null || rows.Length == 0)
                return Array.Empty<Point>();
            var points = new Point[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                points[i] = new Point(cols.LArr[i], rows.LArr[i]);
            return points;
        }

        public byte[] GetMaskData()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            HOperatorSet.SmallestRectangle1(_region, out var r1, out var c1, out var r2, out var c2);
            int maskW = (int)(c2.L - c1.L + 1);
            int maskH = (int)(r2.L - r1.L + 1);
            using var binImage = _region.RegionToBin(255, 0, maskW, maskH);
            HOperatorSet.GetImagePointer1(binImage, out var ptr, out _, out var w, out var h);
            var bytes = new byte[(int)w.L * (int)h.L];
            System.Runtime.InteropServices.Marshal.Copy(ptr, bytes, 0, bytes.Length);
            return bytes;
        }

        public IVisionRegion Clone()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new HalRegion(_region.Clone());
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _region.Dispose();
            }
        }

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("面积", PixelCount),
            };
        }
    }
}