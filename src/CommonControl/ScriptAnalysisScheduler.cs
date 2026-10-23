using System;
using System.Threading;
using System.Threading.Tasks;

namespace CommonControl;

/// <summary>每会话最多一项分析；交互取消正在运行的合作式后台作业，后台不得饿死当前补全。</summary>
internal sealed class ScriptAnalysisScheduler : IDisposable
{
    private readonly SemaphoreSlim _slot = new(1, 1);
    private readonly object _gate = new();
    private CancellationTokenSource? _background;
    private int _interactivePending;
    private readonly CancellationTokenSource _lifetime = new();
    private TaskCompletionSource _idle = Completed();
    private int _operations;
    private bool _disposed;
    private static TaskCompletionSource Completed()
    { var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); signal.SetResult(); return signal; }
    internal Task WhenIdle { get { lock (_gate) return _idle.Task; } }
    public async Task<T> RunAsync<T>(bool interactive, Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        CancellationToken lifetime;
        lock (_gate)
        {
            if (_disposed) throw new OperationCanceledException("编辑会话已结束。");
            lifetime = _lifetime.Token;
            if (_operations++ == 0) _idle = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime);
        token = linked.Token;
        var acquired = false;
        CancellationTokenSource? low = null;
        try
        {
            if (interactive)
            {
                Interlocked.Increment(ref _interactivePending);
                lock (_gate) _background?.Cancel();
            }
            if (!interactive)
                while (Volatile.Read(ref _interactivePending) > 0) await Task.Delay(10, token).ConfigureAwait(false);
            await _slot.WaitAsync(token).ConfigureAwait(false);
            acquired = true;
            if (!interactive)
            {
                low = CancellationTokenSource.CreateLinkedTokenSource(token);
                lock (_gate) { _background = low; if (Volatile.Read(ref _interactivePending) > 0) low.Cancel(); }
            }
            var workToken = low?.Token ?? token;
            return await Task.Run(() => action(workToken), workToken).ConfigureAwait(false);
        }
        finally
        {
            if (interactive) Interlocked.Decrement(ref _interactivePending);
            if (low != null) { lock (_gate) { if (ReferenceEquals(_background, low)) _background = null; } low.Dispose(); }
            if (acquired) _slot.Release();
            lock (_gate) if (--_operations == 0) _idle.TrySetResult();
        }
    }
    public void Dispose()
    {
        Task idle;
        lock (_gate) { if (_disposed) return; _disposed = true; idle = _idle.Task; }
        _lifetime.Cancel();
        _ = idle.ContinueWith(_ => { _lifetime.Dispose(); _slot.Dispose(); }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
