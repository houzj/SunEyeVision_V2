using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.ImageSource.Runtime;

/// <summary>Tool-owned stitch history. Every published image is an independent copy.</summary>
internal sealed class ImageSourceStitchState : IDisposable
{
    private readonly object _gate = new();
    private byte[] _buffer = Array.Empty<byte>();
    private int _width;
    private bool _disposed;

    internal IReadOnlyImage Process(IReadOnlyImage image, ImageSourceSettings settings,
        IOutputResourceScope resources, CancellationToken cancellation)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            cancellation.ThrowIfCancellationRequested();
            if (settings.ClearStitch && settings.ClearStitchValue is not (null or 0))
            {
                Array.Clear(_buffer);
                _width = 0;
            }
            if (_width != 0 && _width != image.Width)
                throw new ImageSourceProcessingException(0xE0000502,"拼接图像宽度与前一帧不一致。");
            if (settings.StitchStart + settings.StitchHeight > image.Height)
                throw new ImageSourceProcessingException(0xE0000502,"拼接起点与高度超出当前图像。");
            if (image.Layout.Format is not (PixelLayout.Gray8 or PixelLayout.Rgb24 or PixelLayout.Bgr24))
                throw new ArgumentException("拼接图像要求 MONO8 或三通道格式。");
            var stride = checked(image.Width * image.Layout.BytesPerPixel);
            var prefixLength = checked(stride * settings.StitchHeight);
            var height = checked(image.Height + settings.StitchHeight);
            var layout = new ImageLayout(image.Width, height, stride, image.Layout.Format);
            var output = _buffer.Length == layout.BufferLength ? _buffer.ToArray() : new byte[layout.BufferLength];
            var next = new byte[prefixLength];
            using var pixels = image.AcquirePixels();
            for (var row = 0; row < image.Height; row++)
            {
                cancellation.ThrowIfCancellationRequested();
                pixels.GetRow(row)[..stride].CopyTo(output.AsSpan(prefixLength + row * stride, stride));
            }
            for (var row = 0; row < settings.StitchHeight; row++)
            {
                cancellation.ThrowIfCancellationRequested();
                pixels.GetRow(settings.StitchStart + row)[..stride].CopyTo(next.AsSpan(row * stride, stride));
            }
            var runs = new RegionRun[height];
            for (var row = 0; row < height; row++) runs[row] = new(row, 0, image.Width - 1);
            cancellation.ThrowIfCancellationRequested();
            var domain = resources.CreateRegion(image.EffectiveDomain.Frame, image.Frame, runs);
            var stitched = resources.CreateImage(layout, image.Frame, domain, output);
            cancellation.ThrowIfCancellationRequested();
            // CreateImage copies its pixels. Updating the next prefix cannot alter this publication.
            next.CopyTo(output, 0);
            _buffer = output;
            _width = image.Width;
            return stitched;
        }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; _buffer = Array.Empty<byte>(); _width = 0; }
    }
}
