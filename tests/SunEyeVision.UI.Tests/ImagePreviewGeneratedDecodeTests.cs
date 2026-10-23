using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Shell.Controls.Panels;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ImagePreviewGeneratedDecodeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SunEyeVision-RealWic", Guid.NewGuid().ToString("N"));
    public ImagePreviewGeneratedDecodeTests() => Directory.CreateDirectory(_root);
    private string Write(string name, int width, int height, byte color)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        { pixels[index] = color; pixels[index + 1] = 50; pixels[index + 2] = 90; pixels[index + 3] = 255; }
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        BitmapEncoder encoder = name.EndsWith(".png") ? new PngBitmapEncoder() : new BmpBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        var path = Path.Combine(_root, name); using var stream = File.Create(path); encoder.Save(stream); return path;
    }

    [Theory]
    [InlineData(320, 160, "landscape.png")]
    [InlineData(120, 2400, "portrait.bmp")]
    public void Real_wic_stream_decodes_thumbnails_and_full_images_without_uri_cache_key(int width, int height, string name)
    {
        var path = Write(name, width, height, 20);
        var thumbnail = ImagePreviewControl.LoadThumbnail(path);
        Assert.NotNull(thumbnail); Assert.True(thumbnail!.IsFrozen);
        Assert.InRange(thumbnail.PixelWidth, 1, 100); Assert.InRange(thumbnail.PixelHeight, 1, 100);
        Assert.Same(thumbnail, ImagePreviewControl.LoadThumbnail(path));
        var full = ImagePreviewControl.LoadImageOptimized(path);
        Assert.NotNull(full); Assert.Equal(width, full!.PixelWidth); Assert.Equal(height, full.PixelHeight); Assert.True(full.IsFrozen);
    }

    [Fact]
    public void Real_wic_same_size_same_timestamp_overwrite_changes_thumbnail_and_full_pixels()
    {
        var path = Write("overwrite.bmp", 64, 32, 10);
        var length = new FileInfo(path).Length; var timestamp = File.GetLastWriteTimeUtc(path);
        var oldThumbnail = ImagePreviewControl.LoadThumbnail(path); var oldFull = ImagePreviewControl.LoadImageOptimized(path);
        Assert.NotNull(oldThumbnail); Assert.NotNull(oldFull);
        Write("overwrite.bmp", 64, 32, 220); File.SetLastWriteTimeUtc(path, timestamp);
        Assert.Equal(length, new FileInfo(path).Length);
        var thumbnail = ImagePreviewControl.LoadThumbnail(path); var full = ImagePreviewControl.LoadImageOptimized(path);
        Assert.NotNull(thumbnail); Assert.NotNull(full); Assert.NotSame(oldThumbnail, thumbnail); Assert.NotSame(oldFull, full);
        static byte FirstBlue(BitmapSource bitmap)
        {
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var data = new byte[converted.PixelWidth * converted.PixelHeight * 4];
            converted.CopyPixels(data, converted.PixelWidth * 4, 0); return data[0];
        }
        Assert.Equal(10, FirstBlue(oldThumbnail!)); Assert.Equal(220, FirstBlue(thumbnail!));
        Assert.Equal(10, FirstBlue(oldFull!)); Assert.Equal(220, FirstBlue(full!));
    }

    [Fact]
    public void Empty_thumbnail_reference_remains_a_visible_failure_value()
    {
        Assert.Null(ImagePreviewControl.LoadThumbnail(string.Empty));
        Assert.Null(ImagePreviewControl.LoadThumbnail(Path.Combine(_root, "missing.png")));
    }
    public void Dispose()
    {
        foreach (var path in Directory.EnumerateFiles(_root)) ImagePreviewControl.InvalidateImageCache(path);
        Directory.Delete(_root, true);
    }
}
