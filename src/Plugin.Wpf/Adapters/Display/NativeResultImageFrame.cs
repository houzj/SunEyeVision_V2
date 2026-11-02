using SunEyeVision.Plugin.Contracts;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Display;

/// <summary>One independent pixel lease; only its private display copy receives Domain alpha.</summary>
internal sealed class NativeResultImageFrame : IImageData, IDisposable
{
    private IImagePixelReadLease? _pixels;
    private readonly object _gate = new();
    private bool _retired;
    private int _reads;
    private byte[] _buffer = Array.Empty<byte>();
    internal ImageFrameAddress Frame { get; }
    internal CoordinateFrame Coordinates { get; }
    internal ImageLayout OriginalLayout { get; }
    internal IReadOnlyList<RegionRun> Domain { get; }
    public int Width => OriginalLayout.Width;
    public int Height => OriginalLayout.Height;
    public string PixelFormat => "Bgra32";
    public string MemoryAddress => "";
    public byte[] Buffer { get { lock (_gate) return !_retired && _pixels is not null ? _buffer : throw new ObjectDisposedException(nameof(NativeResultImageFrame)); }
        set => throw new InvalidOperationException("显示缓冲由视口持有，不能替换规范像素。"); }

    internal NativeResultImageFrame(IReadOnlyImage image, NativeResultRasterLimits limits, Func<bool> current)
    {
        ArgumentNullException.ThrowIfNull(image); limits.Validate(); NativeResultRaster.CheckCurrent(current);
        ResourceContractRules.ValidateImage(image);
        Frame = image.Frame; OriginalLayout = image.Layout;
        var domain = image.EffectiveDomain; Coordinates = domain.Frame;
        var length = NativeResultRaster.BufferLength(Width, Height, limits);
        var pixels = image.AcquirePixels();
        try
        {
            NativeResultRaster.CheckCurrent(current);
            if (pixels.Frame != Frame || pixels.Layout != OriginalLayout || pixels.EffectiveDomain.SourceFrame != Frame
                || pixels.EffectiveDomain.Frame != Coordinates)
                throw new ArgumentException("图像与像素租约的Frame/Layout/Domain不一致。");
            var runs = NativeResultRaster.Read(domain, Frame, Coordinates, Width, Height, limits, current);
            if (!runs.SequenceEqual(NativeResultRaster.ReadOwned(pixels.AcquireDomainReader(), Frame, Coordinates, Width, Height, limits, current)))
                throw new ArgumentException("实际像素Domain与冻结图像Domain不一致。");
            if (!ReferenceEquals(domain, pixels.EffectiveDomain)
                && !runs.SequenceEqual(NativeResultRaster.Read(pixels.EffectiveDomain, Frame, Coordinates, Width, Height, limits, current)))
                throw new ArgumentException("像素租约Domain元数据与实际行程不一致。");
            var buffer = new byte[length];
            var channels = OriginalLayout.Format switch { PixelLayout.Gray8 => 1, PixelLayout.Bgr24 => 3, _ => 4 };
            // Keep original RGB even outside Domain: the original mouse status reads this buffer.
            // Domain changes display alpha only; ReadPixel additionally preserves the original format and A.
            for (var row = 0; row < Height; row++)
            {
                NativeResultRaster.CheckCurrent(current);
                var source = pixels.GetRow(row);
                if (source.Length != OriginalLayout.RowStrideBytes) throw new ArgumentException("像素行长度与冻结stride不一致。");
                for (var x = 0; x < Width; x++)
                {
                    var from = x * channels; var to = (row * Width + x) * 4;
                    buffer[to] = source[from]; buffer[to + 1] = source[from + (channels == 1 ? 0 : 1)];
                    buffer[to + 2] = source[from + (channels == 1 ? 0 : 2)];
                }
            }
            foreach (var run in runs)
            {
                NativeResultRaster.CheckCurrent(current);
                var source = channels == 4 ? pixels.GetRow(run.Row) : default;
                for (var x = run.FirstColumn; x <= run.LastColumnInclusive; x++)
                    buffer[(run.Row * Width + x) * 4 + 3] = channels == 4 ? source[x * 4 + 3] : (byte)255;
            }
            NativeResultRaster.CheckCurrent(current);
            Domain = Array.AsReadOnly(runs); _buffer = buffer; _pixels = pixels;
        }
        catch (Exception failure)
        {
            try { pixels.Dispose(); } catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    internal byte[] ReadPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) throw new ArgumentOutOfRangeException(nameof(x));
        return ReadOriginal(pixels =>
        {
            var channels = OriginalLayout.Format switch { PixelLayout.Gray8 => 1, PixelLayout.Bgr24 => 3, _ => 4 };
            return pixels.GetRow(y).Slice(x * channels, channels).ToArray();
        });
    }

    internal BitmapSource PrepareOriginalBitmap() => ReadOriginal(pixels =>
    {
        var layout = OriginalLayout;
        var format = layout.Format switch
        {
            PixelLayout.Gray8 => PixelFormats.Gray8,
            PixelLayout.Bgr24 => PixelFormats.Bgr24,
            PixelLayout.Bgra32 => PixelFormats.Bgra32,
            _ => throw new NotSupportedException("原图像素格式无法导出。")
        };
        var bytes = new byte[checked(layout.RowStrideBytes * layout.Height)];
        for (var row = 0; row < layout.Height; row++)
        {
            var source = pixels.GetRow(row);
            if (source.Length != layout.RowStrideBytes) throw new InvalidOperationException("原图像素行长度已改变。");
            source.CopyTo(bytes.AsSpan(row * layout.RowStrideBytes, layout.RowStrideBytes));
        }
        var result = BitmapSource.Create(layout.Width, layout.Height, 96, 96, format, null, bytes, layout.RowStrideBytes);
        result.Freeze(); return result;
    });

    private T ReadOriginal<T>(Func<IImagePixelReadLease, T> read)
    {
        IImagePixelReadLease pixels;
        lock (_gate)
        { pixels = !_retired && _pixels is not null ? _pixels : throw new ObjectDisposedException(nameof(NativeResultImageFrame)); _reads++; }
        T? result = default; Exception? failure = null;
        try { result = read(pixels); }
        catch (Exception error) { failure = error; }
        IImagePixelReadLease? release = null;
        lock (_gate) { --_reads; if (_retired && _reads == 0) { release = _pixels; _pixels = null; } }
        try { release?.Dispose(); } catch (Exception cleanup) { failure = failure is null ? cleanup : new AggregateException(failure, cleanup); }
        if (failure is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return result!;
    }
    public void Dispose()
    {
        IImagePixelReadLease? pixels = null;
        lock (_gate)
        {
            if (_retired) return; _retired = true; _buffer = Array.Empty<byte>();
            if (_reads == 0) { pixels = _pixels; _pixels = null; }
        }
        pixels?.Dispose();
    }
}
