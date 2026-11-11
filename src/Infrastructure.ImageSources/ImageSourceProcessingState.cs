namespace SunEyeVision.Infrastructure.ImageSources;

/// <summary>Retains one tool-owned state across calls of the same source lifetime.</summary>
internal sealed class ImageSourceProcessingState : IDisposable
{
    private readonly object _gate = new();
    private IDisposable? _value;
    private int _readers = 1;
    private bool _retired;
    internal CancellationTokenRegistration Retirement;

    internal IDisposable Acquire()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_retired, this);
            _readers = checked(_readers + 1);
            return new Reader(this);
        }
    }
    internal T Get<T>(Func<T> create) where T : class, IDisposable
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_readers == 0, this);
            _value ??= create() ?? throw new InvalidOperationException("图像源处理状态不能为空。");
            return _value as T ?? throw new InvalidOperationException("图像源处理状态的类型已变化。");
        }
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_retired) return;
            _retired = true;
        }
        Release();
    }
    private void Release()
    {
        IDisposable? release = null;
        lock (_gate)
            if (--_readers == 0) { release = _value; _value = null; }
        release?.Dispose();
    }
    private sealed class Reader(ImageSourceProcessingState owner) : IDisposable
    {
        private ImageSourceProcessingState? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
