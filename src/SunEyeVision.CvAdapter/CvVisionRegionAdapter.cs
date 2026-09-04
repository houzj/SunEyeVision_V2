using System;
using System.Linq;
using System.Windows;
using OpenCvSharp;
using SunEyeVision.CvEngine;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using CvRegion = SunEyeVision.CvEngine.CvRegion;
using WpfRect = System.Windows.Rect;
using WpfPoint = System.Windows.Point;

namespace SunEyeVision.CvAdapter
{
    /// <summary>
    /// Cv 区域适配器：把 CvEngine 纯区域（<see cref="CvRegion"/>，掩码载体）桥接为 SDK 契约
    /// <see cref="IVisionRegion"/>。CvEngine 作为纯算法库不实现 SDK 接口（对称架构），
    /// 本类在适配层完成包装；生命周期由调用方管理，Dispose 转交被包装的 CvRegion。
    /// </summary>
    public sealed class CvVisionRegionAdapter : IVisionRegion
    {
        private readonly CvRegion _region;
        private bool _disposed;

        public CvVisionRegionAdapter(CvRegion region)
        {
            _region = region ?? throw new ArgumentNullException(nameof(region));
        }

        public CvRegion Unwrap() => _region;

        public int PixelCount
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _region.PixelCount;
            }
        }

        public WpfRect BoundingBox
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                var mask = _region.Mask;
                if (mask == null || mask.Empty())
                    return WpfRect.Empty;
                var rect = Cv2.BoundingRect(mask);
                return new WpfRect(rect.X, rect.Y, rect.Width, rect.Height);
            }
        }

        public bool IsEmpty
        {
            get
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                return _region.IsEmpty;
            }
        }

        public WpfPoint[] GetContour()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var mask = _region.Mask;
            if (mask == null || mask.Empty())
                return Array.Empty<WpfPoint>();
            var contours = Cv2.FindContoursAsArray(mask, RetrievalModes.External, ContourApproximationModes.ApproxNone);
            if (contours.Length == 0)
                return Array.Empty<WpfPoint>();
            return contours[0].Select(p => new WpfPoint(p.X, p.Y)).ToArray();
        }

        public byte[] GetMaskData()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var mask = _region.Mask;
            if (mask == null || mask.Empty())
                return Array.Empty<byte>();
            var bytes = new byte[mask.Total() * mask.ElemSize()];
            System.Runtime.InteropServices.Marshal.Copy(mask.Data, bytes, 0, bytes.Length);
            return bytes;
        }

        public IVisionRegion Clone()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return new CvVisionRegionAdapter(_region.CopyObj());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _region.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
