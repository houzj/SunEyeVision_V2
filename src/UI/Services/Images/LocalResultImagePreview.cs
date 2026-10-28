using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.UI.Services.Thumbnail;

namespace SunEyeVision.UI.Services.Images;

/// <summary>Display-only decoded local raster. This owner is neither a run result nor proof of a node input.</summary>
internal sealed class LocalResultImagePreview : IDisposable
{
    private FrozenImageResource? _image;
    public IReadOnlyImage Image => Volatile.Read(ref _image) ?? throw new ObjectDisposedException(nameof(LocalResultImagePreview));
    public string FilePath { get; }
    public string ContentVersion { get; }
    private LocalResultImagePreview(string path, string version, FrozenImageResource image)
    { FilePath = path; ContentVersion = version; _image = image; }

    public static Task<LocalResultImagePreview> LoadAsync(string path, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var absolute = Path.GetFullPath(path);
        return Task.Run(() => Decode(absolute, token), token);
    }
    private static LocalResultImagePreview Decode(string path, CancellationToken token)
    {
        FrozenImageResource? image = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var version = ImageCache.ReadVersion(path);
            token.ThrowIfCancellationRequested();
            var decoded = LocalImageAssetCache.LoadImageOptimized(path)
                ?? throw new InvalidDataException("本地文件不能解码为显示图像，或解码期间内容已变化。");
            token.ThrowIfCancellationRequested();
            var target = TargetFormat(decoded);
            BitmapSource raster = decoded;
            if (decoded.Format != target)
            {
                var converted = new FormatConvertedBitmap(decoded, target, null, 0);
                converted.Freeze(); raster = converted;
            }
            var bytesPerPixel = target == PixelFormats.Gray8 ? 1 : target == PixelFormats.Bgr24 ? 3 : 4;
            var stride = checked((checked(raster.PixelWidth * bytesPerPixel) + 3) & ~3);
            var format = bytesPerPixel == 1 ? PixelLayout.Gray8 : bytesPerPixel == 3 ? PixelLayout.Bgr24 : PixelLayout.Bgra32;
            var layout = new ImageLayout(raster.PixelWidth, raster.PixelHeight, stride, format);
            var pixels = new byte[layout.BufferLength];
            raster.CopyPixels(pixels, stride, 0);
            token.ThrowIfCancellationRequested();
            if (!StringComparer.Ordinal.Equals(version, ImageCache.ReadVersion(path)))
                throw new IOException("本地文件内容在解码期间已变化，请重新选择。");
            token.ThrowIfCancellationRequested();
            var frame = new ImageFrameAddress(Guid.NewGuid(), "local-preview:" + path + "#" + version);
            var coordinates = new CoordinateFrame("local-preview:" + frame.FrameId.ToString("N"), CoordinateDomain.Image, CoordinateUnit.Pixel);
            // The file decoder creates a new complete raster, rather than adapting an unknown algorithm Domain.
            // Transparency remains pixel alpha; it does not silently erase valid raster positions from the Domain.
            var domain = new RegionRun[layout.Height];
            for (var row = 0; row < domain.Length; row++)
            {
                if ((row & 255) == 0) token.ThrowIfCancellationRequested();
                domain[row] = new RegionRun(row, 0, layout.Width - 1);
            }
            image = new(layout, frame, coordinates, pixels, domain);
            token.ThrowIfCancellationRequested();
            var owner = new LocalResultImagePreview(path, version, image); image = null;
            return owner;
        }
        finally { image?.Dispose(); }
    }
    private static PixelFormat TargetFormat(BitmapSource source)
    {
        var format = source.Format;
        var palette = source.Palette;
        var indexed = format == PixelFormats.Indexed1 || format == PixelFormats.Indexed2 || format == PixelFormats.Indexed4 || format == PixelFormats.Indexed8;
        var paletteAlpha = indexed && palette?.Colors.Any(color => color.A != 255) == true;
        var grayPalette = indexed && palette is not null && palette.Colors.All(color => color.R == color.G && color.G == color.B);
        if (!paletteAlpha && (grayPalette || format == PixelFormats.Gray8 || format == PixelFormats.Gray16 || format == PixelFormats.Gray32Float
            || format == PixelFormats.BlackWhite || format == PixelFormats.Gray2 || format == PixelFormats.Gray4)) return PixelFormats.Gray8;
        var alpha = paletteAlpha || format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32 || format == PixelFormats.Rgba64
            || format == PixelFormats.Prgba64 || format == PixelFormats.Rgba128Float || format == PixelFormats.Prgba128Float;
        return alpha ? PixelFormats.Bgra32 : PixelFormats.Bgr24;
    }
    public void Dispose() => Interlocked.Exchange(ref _image, null)?.Dispose();
}
