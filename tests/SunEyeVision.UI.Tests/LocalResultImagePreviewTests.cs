using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;
using SunEyeVision.UI.Services.Images;
using Xunit;

namespace SunEyeVision.UI.Tests;

[Collection("Editor commit UI")]
public sealed class LocalResultImagePreviewTests
{
    private static string DirectoryForCase()
    {
        var host = typeof(LocalResultImagePreviewTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == "HostDebugRoot").Value!;
        var root = Environment.GetEnvironmentVariable("S2_CASE_ROOT") ?? Path.Combine(host, "..", "..", "_validation", "LocalResultImagePreview");
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); return directory;
    }
    private static void Save(string path, PixelFormat format, int width, int height, int stride, byte[] pixels, BitmapPalette? palette = null)
    {
        var bitmap = BitmapSource.Create(width, height, 96, 96, format, palette, pixels, stride); bitmap.Freeze();
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    [Fact]
    public void Real_gray_file_preview_keeps_independent_pixel_and_domain_leases_after_decoder_owner_exits()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        var directory = DirectoryForCase(); var file = Path.Combine(directory, "gray.png");
        try
        {
            Save(file, PixelFormats.Gray8, 3, 2, 4, new byte[] { 1, 2, 3, 99, 4, 5, 6, 88 });
            using var decoded = LocalResultImagePreview.LoadAsync(file).GetAwaiter().GetResult();
            var image = decoded.Image; var frame = image.Frame;
            Assert.Equal(PixelLayout.Gray8, image.Layout.Format); Assert.Equal(4, image.Layout.RowStrideBytes);
            Assert.Null(frame.ProducerRun); Assert.Null(frame.SourcePortId); Assert.Null(frame.SchemaRevision);
            Assert.Equal(frame, image.EffectiveDomain.SourceFrame);
            using var pixels = image.AcquirePixels(); using var domain = pixels.AcquireDomainReader();
            Assert.Equal(new[] { new RegionRun(0, 0, 2), new RegionRun(1, 0, 2) }, domain.ReadRuns(0, 4096).ToArray());
            using var pane = new ResultImagePane(new ImageViewBox());
            var current = true;
            Assert.True(pane.TryPresentPreview(image, () => current, out var error), error);
            Assert.Null(pane.BorrowCurrentImage()); // A file preview is not proof of a node's actual input.
            decoded.Dispose(); decoded.Dispose();
            Assert.Throws<ObjectDisposedException>(() => decoded.Image);
            Assert.Throws<ObjectDisposedException>(() => image.CopyPixels());
            Assert.Equal(new byte[] { 1, 2, 3 }, pixels.GetRow(0).ToArray().Take(3));
            Assert.Equal(new byte[] { 4, 5, 6 }, pixels.GetRow(1).ToArray().Take(3));
            Assert.Equal(new byte[] { 2 }, pane.Viewport.ReadPixel(1, 0));
            Assert.Equal(frame, pane.CurrentFrame!.Frame); Assert.Equal(2, domain.RunCount);
            current = false; pane.RefreshCurrentness(); Assert.Null(pane.CurrentFrame);
            pixels.Dispose(); Assert.Throws<ObjectDisposedException>(() => pixels.GetRow(0).ToArray());
            Assert.Equal(2, domain.RunCount); domain.Dispose(); Assert.Throws<ObjectDisposedException>(() => domain.RunCount);
        }
        finally { Directory.Delete(directory, true); }
    });
    [Fact]
    public void Real_color_and_alpha_files_keep_channel_order_alpha_and_full_decoded_raster_domain()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        var directory = DirectoryForCase();
        try
        {
            var colorPath = Path.Combine(directory, "color.png");
            Save(colorPath, PixelFormats.Bgr24, 2, 1, 8, new byte[] { 10, 20, 30, 40, 50, 60, 0, 0 });
            using var color = LocalResultImagePreview.LoadAsync(colorPath).GetAwaiter().GetResult();
            Assert.True(color.Image.Layout.Format is PixelLayout.Bgr24 or PixelLayout.Bgra32);
            var bytes = color.Image.CopyPixels(); Assert.Equal(new byte[] { 10, 20, 30 }, bytes.Take(3));
            using var pane = new ResultImagePane(new ImageViewBox());
            Assert.True(pane.TryPresentPreview(color.Image, () => true, out var error), error);
            Assert.Equal(new byte[] { 10, 20, 30 }, pane.Viewport.ReadPixel(0, 0)!.Take(3));
            var alphaPath = Path.Combine(directory, "alpha.png");
            Save(alphaPath, PixelFormats.Bgra32, 2, 1, 8, new byte[] { 20, 40, 60, 128, 0, 0, 0, 0 });
            using var alpha = LocalResultImagePreview.LoadAsync(alphaPath).GetAwaiter().GetResult();
            Assert.Equal(PixelLayout.Bgra32, alpha.Image.Layout.Format);
            Assert.Equal(new byte[] { 20, 40, 60, 128, 0, 0, 0, 0 }, alpha.Image.CopyPixels());
            using var actualDomain = alpha.Image.EffectiveDomain.AcquireReader();
            Assert.Equal(new[] { new RegionRun(0, 0, 1) }, actualDomain.ReadRuns(0, 4096).ToArray());
            Assert.True(pane.TryPresentPreview(alpha.Image, () => true, out error), error);
            Assert.Null(pane.BorrowCurrentImage()); Assert.Equal(new byte[] { 20, 40, 60, 128 }, pane.Viewport.ReadPixel(0, 0));
            var palettePath = Path.Combine(directory, "palette.png");
            Save(palettePath, PixelFormats.Indexed8, 2, 1, 4, new byte[] { 1, 0, 0, 0 }, new BitmapPalette(new[] { Colors.Transparent, Colors.Red }));
            using var indexed = LocalResultImagePreview.LoadAsync(palettePath).GetAwaiter().GetResult();
            Assert.Equal(PixelLayout.Bgra32, indexed.Image.Layout.Format);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, indexed.Image.CopyPixels().Take(4));
            Assert.Equal(0, indexed.Image.CopyPixels()[7]);
        }
        finally { Directory.Delete(directory, true); }
    });
    [Fact]
    public void Same_path_changed_bytes_and_different_paths_never_reuse_a_physical_frame()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        var directory = DirectoryForCase(); var file = Path.Combine(directory, "version.png");
        try
        {
            Save(file, PixelFormats.Gray8, 1, 1, 4, new byte[] { 10, 0, 0, 0 }); var stamp = File.GetLastWriteTimeUtc(file);
            using var first = LocalResultImagePreview.LoadAsync(file).GetAwaiter().GetResult();
            using var firstPixels = first.Image.AcquirePixels();
            Save(file, PixelFormats.Gray8, 1, 1, 4, new byte[] { 11, 0, 0, 0 }); File.SetLastWriteTimeUtc(file, stamp);
            using var second = LocalResultImagePreview.LoadAsync(file).GetAwaiter().GetResult();
            Assert.NotEqual(first.ContentVersion, second.ContentVersion); Assert.NotEqual(first.Image.Frame, second.Image.Frame);
            Assert.Equal(10, firstPixels.GetRow(0)[0]); Assert.Equal(11, second.Image.CopyPixels()[0]);
            var otherPath = Path.Combine(directory, "same-bytes-other-path.png"); File.Copy(file, otherPath);
            using var other = LocalResultImagePreview.LoadAsync(otherPath).GetAwaiter().GetResult();
            Assert.Equal(second.ContentVersion, other.ContentVersion); Assert.NotEqual(second.Image.Frame, other.Image.Frame);
            Assert.NotEqual(second.Image.Frame.SourceId, other.Image.Frame.SourceId);
        }
        finally { Directory.Delete(directory, true); }
    });
    [Fact]
    public void Cancelled_missing_and_corrupt_file_loads_return_no_fake_result_and_next_decode_still_works()
        => ImageThresholdProductionEditorTests.Sta(() =>
    {
        var directory = DirectoryForCase(); var file = Path.Combine(directory, "cancel.png");
        try
        {
            Save(file, PixelFormats.Gray8, 1, 1, 4, new byte[] { 12, 0, 0, 0 });
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => LocalResultImagePreview.LoadAsync(file, cancellation.Token).GetAwaiter().GetResult());
            Assert.Throws<FileNotFoundException>(() => LocalResultImagePreview.LoadAsync(Path.Combine(directory, "missing.png")).GetAwaiter().GetResult());
            var corrupt = Path.Combine(directory, "invalid.png"); File.WriteAllText(corrupt, "not an image");
            Assert.Throws<InvalidDataException>(() => LocalResultImagePreview.LoadAsync(corrupt).GetAwaiter().GetResult());
            using var valid = LocalResultImagePreview.LoadAsync(file).GetAwaiter().GetResult();
            Assert.Equal(12, valid.Image.CopyPixels()[0]); valid.Dispose(); valid.Dispose();
        }
        finally { Directory.Delete(directory, true); }
    });
}
