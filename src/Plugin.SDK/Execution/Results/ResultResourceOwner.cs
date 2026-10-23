using System;
using System.Threading;

namespace SunEyeVision.Plugin.SDK.Execution.Results;

/// <summary>仅供显式拥有资源的结果使用。每个长期持有者取得租约，最后一个释放者回收。</summary>
public sealed class ResultResourceOwner : IDisposable
{
    private readonly object _gate = new();
    private Action? _release;
    private int _references = 1;
    private int _ownerReleased;
    public ResultResourceOwner(Action release) => _release = release ?? throw new ArgumentNullException(nameof(release));
    public IDisposable Retain()
    {
        lock (_gate)
        {
            if (_references == 0) throw new ObjectDisposedException(nameof(ResultResourceOwner));
            _references++;
            return new Lease(this);
        }
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _ownerReleased, 1) == 0) Release();
    }
    private void Release()
    {
        Action? release = null;
        lock (_gate)
        {
            if (--_references == 0) { release = _release; _release = null; }
        }
        release?.Invoke();
    }
    private sealed class Lease(ResultResourceOwner owner) : IDisposable
    {
        private ResultResourceOwner? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Release();
    }
}
