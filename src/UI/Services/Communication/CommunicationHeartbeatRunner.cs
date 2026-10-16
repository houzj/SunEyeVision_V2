using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.UI.Services.Communication;

public sealed record CommunicationHeartbeatJob(Guid Id, string TargetKey, int IntervalMs,
    bool Alternate, Func<bool, CancellationToken, Task<bool>> SendAsync);

/// <summary>Application-owned non-overlapping loops. No window, device configuration or workflow scheduling.</summary>
public sealed class CommunicationHeartbeatRunner : IAsyncDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _cancellation;
    private Task _lifecycle = Task.CompletedTask;
    private bool _disposed;
    public event Action<Guid, string>? StatusChanged;

    public void Update(IEnumerable<CommunicationHeartbeatJob> jobs)
    {
        var snapshot = jobs.ToArray();
        if (snapshot.Any(job => job.IntervalMs < 1)) throw new ArgumentOutOfRangeException(nameof(jobs), "心跳间隔必须大于 0ms。");
        if (snapshot.Select(job => job.Id).Distinct().Count() != snapshot.Length ||
            snapshot.Select(job => job.TargetKey).Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
            throw new ArgumentException("心跳 ID 或设备目标不能重复。", nameof(jobs));
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var oldCancellation = _cancellation;
            oldCancellation?.Cancel();
            var previous = _lifecycle;
            _cancellation = new CancellationTokenSource();
            var token = _cancellation.Token;
            _lifecycle = Task.Run(async () =>
            {
                try { await previous.ConfigureAwait(false); }
                finally { oldCancellation?.Dispose(); }
                if (!token.IsCancellationRequested)
                    await Task.WhenAll(snapshot.Select(job => RunAsync(job, token))).ConfigureAwait(false);
            });
        }
    }

    private async Task RunAsync(CommunicationHeartbeatJob job, CancellationToken token)
    {
        var second = false;
        try
        {
            // First send waits a full interval; send completion precedes the next interval.
            while (true)
            {
                await Task.Delay(job.IntervalMs, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                try
                {
                    if (await job.SendAsync(second, token).ConfigureAwait(false))
                    {
                        token.ThrowIfCancellationRequested();
                        Publish(job.Id, "发送成功");
                        if (job.Alternate) second = !second;
                    }
                    else if (!token.IsCancellationRequested) Publish(job.Id, "等待连接或接收端");
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex) { if (!token.IsCancellationRequested) Publish(job.Id, ex.Message); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    internal void Publish(Guid id, string status)
    {
        // A diagnostic/UI subscriber must never stop the application-owned sender.
        foreach (var handler in StatusChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
            try { ((Action<Guid, string>)handler)(id, status); } catch { }
    }

    public async Task StopAsync()
    {
        Task pending;
        lock (_gate) { _cancellation?.Cancel(); pending = _lifecycle; }
        await pending.ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        await StopAsync().ConfigureAwait(false);
        _cancellation?.Dispose();
    }
}
