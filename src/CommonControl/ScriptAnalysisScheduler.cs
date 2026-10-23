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
    public async Task<T> RunAsync<T>(bool interactive, Func<CancellationToken, Task<T>> action, CancellationToken token)
    {
        if (interactive)
        {
            Interlocked.Increment(ref _interactivePending);
            lock (_gate) _background?.Cancel();
        }
        var acquired = false;
        CancellationTokenSource? low = null;
        try
        {
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
        }
    }
    public void Dispose() { lock (_gate) _background?.Cancel(); }
}
