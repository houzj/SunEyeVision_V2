using System.IO;
using System.Security.Cryptography;
using System.Windows.Media.Imaging;

namespace SunEyeVision.UI.Services.Thumbnail;

public sealed record PreviewBitmapCacheStats(long DecodedBytes, int Entries, int InFlight, long Decodes, int Waiters);

/// <summary>Content-versioned, byte-bounded cache with independent waiter cancellation.</summary>
public sealed class PreviewBitmapCache
{
    private sealed record Key(string Path, string Hash);
    private sealed class KeyComparer : IEqualityComparer<Key>
    {
        public bool Equals(Key? x, Key? y) => x is not null && y is not null
            && StringComparer.OrdinalIgnoreCase.Equals(x.Path, y.Path) && x.Hash == y.Hash;
        public int GetHashCode(Key key) => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(key.Path), key.Hash);
    }
    private sealed record Entry(Key Key, BitmapSource Bitmap, long Bytes);
    private sealed class Flight
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task<BitmapSource?> Work { get; set; } = null!;
        public int Waiters { get; set; }
        public bool Completed { get; set; }
    }
    private readonly object _gate = new();
    private readonly Dictionary<Key, Flight> _flights = new(new KeyComparer());
    private readonly Dictionary<string, LinkedListNode<Entry>> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<Entry> _lru = new();
    private readonly SemaphoreSlim _slots;
    private readonly Func<string, BitmapSource> _decode;
    private readonly long _byteBudget;
    private readonly long _entryByteBudget;
    private readonly int _entryLimit;
    private long _bytes;
    private long _decodes;
    private int _workers;

    public PreviewBitmapCache(Func<string, BitmapSource> decode, long byteBudget = 16 * 1024 * 1024,
        int entryLimit = 128, long entryByteBudget = 256 * 1024, int concurrency = 4)
    {
        if (byteBudget <= 0 || entryLimit <= 0 || entryByteBudget <= 0 || concurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(byteBudget));
        _decode = decode; _byteBudget = byteBudget; _entryLimit = entryLimit; _entryByteBudget = entryByteBudget;
        _slots = new(concurrency, concurrency);
    }

    public PreviewBitmapCacheStats Statistics
    {
        get { lock (_gate) return new(_bytes, _cache.Count, _workers, _decodes, _flights.Values.Sum(flight => flight.Waiters)); }
    }

    private static async Task<Key> ReadKeyAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return new(path, Convert.ToHexString(await SHA256.HashDataAsync(stream, token)));
    }

    public async Task<BitmapSource?> LoadAsync(string path, CancellationToken token = default)
    {
        path = System.IO.Path.GetFullPath(path);
        Key key;
        await _slots.WaitAsync(token).ConfigureAwait(false);
        try { key = await ReadKeyAsync(path, token).ConfigureAwait(false); }
        finally { _slots.Release(); }
        Flight flight;
        lock (_gate)
        {
            token.ThrowIfCancellationRequested();
            if (_cache.TryGetValue(path, out var cached))
            {
                if (cached.Value.Key.Hash == key.Hash)
                {
                    _lru.Remove(cached); _lru.AddFirst(cached);
                    return cached.Value.Bitmap;
                }
                RemoveCached(path);
            }
            if (!_flights.TryGetValue(key, out flight!))
            {
                flight = new Flight();
                _flights.Add(key, flight);
                _workers++;
                flight.Work = Task.Run(() => DecodeAsync(key, flight));
            }
            flight.Waiters++;
        }
        try { return await flight.Work.WaitAsync(token).ConfigureAwait(false); }
        finally
        {
            lock (_gate)
            {
                flight.Waiters--;
                if (flight.Waiters == 0)
                {
                    if (flight.Completed) flight.Cancellation.Dispose();
                    else
                    {
                        flight.Cancellation.Cancel();
                        if (_flights.TryGetValue(key, out var active) && ReferenceEquals(active, flight)) _flights.Remove(key);
                    }
                }
            }
        }
    }

    private async Task<BitmapSource?> DecodeAsync(Key key, Flight flight)
    {
        var token = flight.Cancellation.Token;
        try
        {
            await _slots.WaitAsync(token).ConfigureAwait(false);
            try
            {
                token.ThrowIfCancellationRequested();
                lock (_gate) _decodes++;
                var bitmap = _decode(key.Path);
                if (!bitmap.IsFrozen) bitmap.Freeze();
                var bytes = checked(((long)bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8 * bitmap.PixelHeight);
                if (bytes > _entryByteBudget || bytes > _byteBudget) return null;
                token.ThrowIfCancellationRequested();
                if (await ReadKeyAsync(key.Path, token).ConfigureAwait(false) != key) return null;
                lock (_gate)
                {
                    if (token.IsCancellationRequested || !_flights.TryGetValue(key, out var active) || !ReferenceEquals(active, flight)) return null;
                    RemoveCached(key.Path);
                    _cache[key.Path] = _lru.AddFirst(new Entry(key, bitmap, bytes));
                    _bytes += bytes;
                    while (_bytes > _byteBudget || _cache.Count > _entryLimit) RemoveCached(_lru.Last!.Value.Key.Path);
                }
                return bitmap;
            }
            finally { _slots.Release(); }
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            SunEyeVision.Plugin.SDK.Logging.VisionLogger.Instance.Log(
                SunEyeVision.Plugin.SDK.Logging.LogLevel.Warning, $"缩略图加载失败 | {key.Path} | {ex.Message}", "PreviewBitmapCache");
            return null;
        }
        finally
        {
            lock (_gate)
            {
                flight.Completed = true;
                _workers--;
                if (_flights.TryGetValue(key, out var active) && ReferenceEquals(active, flight)) _flights.Remove(key);
                if (flight.Waiters == 0) flight.Cancellation.Dispose();
            }
        }
    }

    private void RemoveCached(string path)
    {
        if (!_cache.Remove(path, out var node)) return;
        _bytes -= node.Value.Bytes; _lru.Remove(node);
    }

    public void Invalidate(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        path = System.IO.Path.GetFullPath(path);
        lock (_gate)
        {
            RemoveCached(path);
            foreach (var pair in _flights.Where(pair => string.Equals(pair.Key.Path, path, StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                pair.Value.Cancellation.Cancel();
                _flights.Remove(pair.Key);
            }
        }
    }
}
