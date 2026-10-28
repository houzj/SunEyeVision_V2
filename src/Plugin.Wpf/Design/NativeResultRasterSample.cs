using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeResultRasterPreview : UserControl, IDisposable
{
    public NativeResultRasterPreview() => InitializeComponent();
    public void Dispose() { var content = Content; Content = null; (content as IDisposable)?.Dispose(); }
}

/// <summary>Same viewport and exact raster path, with independent managed design data, no Runtime or algorithm.</summary>
public sealed class NativeResultRasterSample : Decorator, IDisposable
{
    private readonly NativeRoiSamples.Image _image = new();
    private readonly NativeResultViewport _viewport = new();
    private readonly NativeResultViewport.RegionLayer _layer;
    private bool _disposed;
    public NativeResultViewport Viewport => _viewport;
    public NativeResultRasterSample()
    {
        _layer = _viewport.CreateRegionLayer(1);
        try
        {
            Child = _viewport.View;
            if (!_viewport.BindImage(_image, () => !_disposed)) throw new InvalidOperationException("独立视口样例未接纳。");
            if (!_layer.Present(_viewport.CurrentFrame!, 0, new DesignMask(_image.Frame), Color.FromArgb(150, 102, 255, 0), () => !_disposed))
                throw new InvalidOperationException("独立精确区域样例未接纳。");
            var receipt = _viewport.CurrentFrame!;
            if (!_viewport.CreateGeometryLayer().Present(receipt, 0,
                    new[] { new NativeGeometryOverlay(_image.Frame,
                        new OrientedRectangleGeometry(receipt.Coordinates, new(320, 160), 380, 160, 0), Colors.Orange, 2) }, () => !_disposed)
                || !_viewport.CreateTextLayer().Present(receipt, 0,
                    new[] { new NativeTextOverlay("Result", new(130, 55), 18, Colors.Lime, 1) }, () => !_disposed))
                throw new InvalidOperationException("独立几何和文字样例未接纳。");
        }
        catch { Dispose(); throw; }
    }
    public void Dispose()
    {
        if (_disposed) return; _disposed = true;
        try { _viewport.Dispose(); } finally { _image.Dispose(); Child = null; }
    }
    private sealed class DesignMask(ImageFrameAddress source) : IReadOnlyRegion
    {
        private static readonly RegionRun[] Runs = Enumerable.Range(80, 160).SelectMany(row => row is >= 120 and < 200
            ? new[] { new RegionRun(row, 130, 209), new RegionRun(row, 430, 509) }
            : new[] { new RegionRun(row, 130, 509) }).ToArray();
        public CoordinateFrame Frame => RoiDeclarations.ImageFrame;
        public ImageFrameAddress SourceFrame => source;
        public IRegionReadLease AcquireReader() => new Reader(this);
        private sealed class Reader(DesignMask mask) : IRegionReadLease
        {
            private bool _disposed;
            private void Check() => ObjectDisposedException.ThrowIf(_disposed, this);
            public CoordinateFrame Frame { get { Check(); return mask.Frame; } }
            public ImageFrameAddress SourceFrame { get { Check(); return mask.SourceFrame; } }
            public bool IsEmpty { get { Check(); return false; } }
            public int RunCount { get { Check(); return Runs.Length; } }
            public ReadOnlyMemory<RegionRun> ReadRuns(int offset, int maximumCount)
            {
                Check();
                if (offset < 0 || offset > Runs.Length || maximumCount is < 1 or > 4096) throw new ArgumentOutOfRangeException(nameof(offset));
                return Runs.AsMemory(offset, Math.Min(maximumCount, Runs.Length - offset));
            }
            public void Dispose() => _disposed = true;
        }
    }
}
