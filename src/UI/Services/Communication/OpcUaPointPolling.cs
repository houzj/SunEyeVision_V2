using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Communications;

namespace SunEyeVision.UI.Services.Communication;

/// <summary>One non-overlapping reader per point. Stop waits for cancellation before replacing a reader.</summary>
internal sealed class OpcUaPointPolling
{
    private readonly Func<string, bool> _connected;
    private readonly Func<string, string, CancellationToken, Task> _read;
    private readonly Action<string, string, string> _error;
    public OpcUaPointPolling(CommunicationRuntimeManager runtime) : this(runtime.IsConnected,
        async (device, node, token) => { await runtime.RefreshOpcUaPointAsync(device, node, token).ConfigureAwait(false); }, runtime.ReportOpcUaPointError) { }
    internal OpcUaPointPolling(Func<string, bool> connected, Func<string, string, CancellationToken, Task> read, Action<string, string, string> error)
    { _connected = connected; _read = read; _error = error; }
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<(string, string), (CancellationTokenSource Cts, Task Task)> _jobs = new();

    public async Task StartAsync(string device, string node, int interval, CancellationToken lifetime)
    {
        if (interval is < 10 or > 600000) throw new ArgumentOutOfRangeException(nameof(interval));
        await _gate.WaitAsync(lifetime).ConfigureAwait(false);
        try
        {
            await StopCoreAsync((device, node)).ConfigureAwait(false);
            var cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            var task = Task.Run(async () =>
            {
                try
                {
                    while (!cts.IsCancellationRequested && _connected(device))
                    {
                        await Task.Delay(interval, cts.Token).ConfigureAwait(false);
                        if (!_connected(device)) break;
                        try { await _read(device, node, cts.Token).ConfigureAwait(false); }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested) { break; }
                        catch (Exception ex) { _error(device, node, ex.Message); }
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested) { }
            }, CancellationToken.None);
            _jobs[(device, node)] = (cts, task);
        }
        finally { _gate.Release(); }
    }

    public async Task StopAsync(string device, string node)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await StopCoreAsync((device, node)).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }

    public async Task StopAllAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            foreach (var job in _jobs.Values) job.Cts.Cancel();
            foreach (var key in new List<(string, string)>(_jobs.Keys)) await StopCoreAsync(key).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    private async Task StopCoreAsync((string, string) key)
    {
        if (!_jobs.Remove(key, out var job)) return;
        job.Cts.Cancel();
        try { await job.Task.ConfigureAwait(false); }
        finally { job.Cts.Dispose(); }
    }
}
