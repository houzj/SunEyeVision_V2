namespace SunEyeVision.Execution;

/// <summary>Pause and stop affect dispatch while in-flight invocations retain their original cancellation identity.</summary>
internal sealed class ExecutionRunControl : IDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _source;
    private TaskCompletionSource? _paused;
    private bool _closed;
    internal ExecutionRunControl(CancellationToken cancellation) => _source = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
    internal CancellationToken Token => _source.Token;
    internal bool Pause()
    {
        lock (_gate)
        {
            if (_closed || _source.IsCancellationRequested || _paused is not null) return false;
            _paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return true;
        }
    }
    internal bool Resume()
    {
        TaskCompletionSource? signal;
        lock (_gate) { if (_closed || _paused is null) return false; signal = _paused; _paused = null; }
        signal.TrySetResult(); return true;
    }
    internal void Stop()
    {
        lock (_gate) { if (_closed) return; }
        try { _source.Cancel(); } catch (ObjectDisposedException) { /* completion won the race */ }
    }
    internal async ValueTask WaitAsync()
    {
        Task? pending;
        lock (_gate) pending = _paused?.Task;
        if (pending is not null) await pending.WaitAsync(_source.Token).ConfigureAwait(false);
        _source.Token.ThrowIfCancellationRequested();
    }
    public void Dispose()
    {
        TaskCompletionSource? signal;
        lock (_gate) { if (_closed) return; _closed = true; signal = _paused; _paused = null; }
        signal?.TrySetResult(); _source.Dispose();
    }
}
