using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Services.Thumbnail;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class PreviewBitmapCacheTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SunEyeVision-PreviewCache", Guid.NewGuid().ToString("N"));
    public PreviewBitmapCacheTests() => Directory.CreateDirectory(_directory);
    private string File(string name, byte value = 1)
    {
        var path = Path.Combine(_directory, name);
        System.IO.File.WriteAllBytes(path, new[] { value });
        return path;
    }
    private static BitmapSource Pixel(byte value, int size = 1)
    {
        var bitmap = BitmapSource.Create(size, size, 96, 96, PixelFormats.Gray8, null,
            Enumerable.Repeat(value, size * size).ToArray(), size);
        bitmap.Freeze(); return bitmap;
    }
    private static async Task WaitForAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(condition(), "缓存任务未在界限内完成");
    }

    [Fact]
    public async Task OneCancelledWaiterDoesNotCancelSharedDecode()
    {
        var path = File("shared.png");
        using var gate = new ManualResetEventSlim();
        var cache = new PreviewBitmapCache(_ => { Assert.True(gate.Wait(TimeSpan.FromSeconds(10))); return Pixel(1); });
        using var cancellation = new CancellationTokenSource();
        var first = cache.LoadAsync(path, cancellation.Token);
        var second = cache.LoadAsync(path);
        try
        {
            await WaitForAsync(() => cache.Statistics.Waiters == 2);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        }
        finally { gate.Set(); }
        var bitmap = await second;
        Assert.NotNull(bitmap); Assert.Same(bitmap, await cache.LoadAsync(path));
        Assert.Equal(1, cache.Statistics.Decodes);
    }

    [Fact]
    public async Task LastWaiterCancellationAndInvalidationPreventLateCacheWrites()
    {
        var path = File("cancel.png");
        using var gate = new ManualResetEventSlim();
        var cache = new PreviewBitmapCache(_ => { Assert.True(gate.Wait(TimeSpan.FromSeconds(10))); return Pixel(1); });
        using var cancellation = new CancellationTokenSource();
        var work = cache.LoadAsync(path, cancellation.Token);
        try
        {
            await WaitForAsync(() => cache.Statistics.Decodes == 1);
            cache.Invalidate(path);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work);
        }
        finally { gate.Set(); }
        await WaitForAsync(() => cache.Statistics.InFlight == 0);
        Assert.Equal(0, cache.Statistics.Entries); Assert.Equal(0, cache.Statistics.DecodedBytes);
    }

    [Fact]
    public async Task SameLengthSameTimestampOverwriteCannotReturnOldPixels()
    {
        var path = File("overwrite.png", 1);
        var timestamp = System.IO.File.GetLastWriteTimeUtc(path);
        var cache = new PreviewBitmapCache(file => Pixel(System.IO.File.ReadAllBytes(file)[0]));
        var old = await cache.LoadAsync(path);
        System.IO.File.WriteAllBytes(path, new byte[] { 2 }); System.IO.File.SetLastWriteTimeUtc(path, timestamp);
        var updated = await cache.LoadAsync(path);
        Assert.NotNull(old); Assert.NotNull(updated); Assert.NotSame(old, updated);
        var pixels = new byte[1]; updated!.CopyPixels(pixels, 1, 0); Assert.Equal(2, pixels[0]);
        Assert.Equal(2, cache.Statistics.Decodes);
    }

    [Fact]
    public async Task ByteBudgetEvictsAndOversizedEntryIsNotRetained()
    {
        var cache = new PreviewBitmapCache(_ => Pixel(1, 10), byteBudget: 150, entryLimit: 20, entryByteBudget: 120);
        await cache.LoadAsync(File("one.png")); await cache.LoadAsync(File("two.png"));
        Assert.Equal(1, cache.Statistics.Entries); Assert.Equal(100, cache.Statistics.DecodedBytes);
        var oversized = new PreviewBitmapCache(_ => Pixel(1, 20), byteBudget: 150, entryByteBudget: 120);
        Assert.Null(await oversized.LoadAsync(File("big.png")));
        Assert.Equal(0, oversized.Statistics.DecodedBytes);
    }

    [Fact]
    public async Task DirectoryPreparationIsOrderedCancellableAndNeverDeletesFiles()
    {
        var b = File("b.PNG"); var a = File("a.bmp"); File("skip.txt");
        Assert.Equal(new[] { a, b }, await ImageFileImport.DirectoryFilesAsync(_directory, CancellationToken.None));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ImageFileImport.DirectoryFilesAsync(_directory, cancellation.Token));
        Assert.Equal(new[] { a }, await ImageFileImport.ExistingFilesAsync(new[] { a, a, "missing" }, CancellationToken.None));
        Assert.True(System.IO.File.Exists(a)); Assert.True(System.IO.File.Exists(b));
    }
    public void Dispose() => Directory.Delete(_directory, true);
}
