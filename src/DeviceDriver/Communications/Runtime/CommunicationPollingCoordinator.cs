using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>Immutable Modbus read plan, detached from persisted address objects and WPF.</summary>
public sealed record ModbusPollAddress(string AddressId, int StartAddress, int Count, int TimeoutMs);

public sealed record ModbusPollResult(
    string CommunicationId, string AddressId, int StartAddress, int Count,
    ushort[] Values, DateTimeOffset Timestamp);

public sealed record ModbusPollBatch(string CommunicationId, long Generation, long PlanVersion,
    IReadOnlyList<ModbusPollResult> Results);

/// <summary>Application-owned polling; window closure does not stop an active device.</summary>
public sealed class CommunicationPollingCoordinator : IAsyncDisposable
{
    private sealed class Job
    {
        public readonly CancellationTokenSource Cancellation = new();
        public ModbusPollAddress[] Addresses = Array.Empty<ModbusPollAddress>();
        public int IntervalMs;
        public Task RunTask = Task.CompletedTask;
        public long Generation;
        public long PlanVersion;
    }

    private readonly CommunicationRuntimeManager _runtime;
    private readonly ConcurrentDictionary<string, Job> _jobs = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<(string Device, string Address), ModbusPollResult> _latest = new();
    private long _nextGeneration;

    public CommunicationPollingCoordinator(CommunicationRuntimeManager runtime) =>
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

    public event EventHandler<ModbusPollBatch>? BatchReceived;

    public IReadOnlyList<ModbusPollResult> GetLatest(string communicationId) =>
        _latest.Where(item => item.Key.Device == communicationId).Select(item => item.Value).ToArray();

    public CancellationToken GetLifetimeToken(string communicationId) =>
        _jobs.TryGetValue(communicationId, out var job) ? job.Cancellation.Token : CancellationToken.None;

    public bool IsCurrentBatch(ModbusPollBatch batch) =>
        _jobs.TryGetValue(batch.CommunicationId, out var job) &&
        job.Generation == batch.Generation && job.PlanVersion == batch.PlanVersion &&
        !job.Cancellation.IsCancellationRequested;

    public void Start(string communicationId, int intervalMs, IEnumerable<ModbusPollAddress> addresses)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(communicationId);
        ArgumentNullException.ThrowIfNull(addresses);
        Stop(communicationId);
        var job = new Job
        {
            IntervalMs = Math.Clamp(intervalMs, 100, 60_000),
            Addresses = addresses.ToArray(),
            Generation = Interlocked.Increment(ref _nextGeneration)
        };
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        job.RunTask = Task.Run(async () =>
        {
            await start.Task.ConfigureAwait(false);
            await RunAsync(communicationId, job).ConfigureAwait(false);
        });
        if (!_jobs.TryAdd(communicationId, job))
        {
            job.Cancellation.Cancel();
            start.TrySetResult();
            throw new InvalidOperationException("通讯设备的轮询任务已存在。");
        }
        start.TrySetResult();
    }

    public void UpdatePlan(string communicationId, int intervalMs, IEnumerable<ModbusPollAddress> addresses)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        if (!_jobs.TryGetValue(communicationId, out var job)) return;
        var plan = addresses.ToArray();
        Volatile.Write(ref job.Addresses, plan);
        Interlocked.Increment(ref job.PlanVersion);
        Volatile.Write(ref job.IntervalMs, Math.Clamp(intervalMs, 100, 60_000));
        var retained = plan.Select(item => item.AddressId).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _latest.Keys.Where(key => key.Device == communicationId && !retained.Contains(key.Address)).ToArray())
            _latest.TryRemove(key, out _);
    }

    public void Stop(string communicationId)
    {
        if (_jobs.TryRemove(communicationId, out var job))
            job.Cancellation.Cancel();
        foreach (var key in _latest.Keys.Where(key => key.Device == communicationId).ToArray())
            _latest.TryRemove(key, out _);
    }

    public async Task StopAllAsync()
    {
        var jobs = _jobs.ToArray();
        foreach (var item in jobs) Stop(item.Key);
        await Task.WhenAll(jobs.Select(item => item.Value.RunTask)).ConfigureAwait(false);
    }

    private async Task RunAsync(string communicationId, Job job)
    {
        try
        {
            while (!job.Cancellation.IsCancellationRequested && _runtime.IsConnected(communicationId))
            {
                var pending = new List<ModbusPollResult>();
                var planVersion = Volatile.Read(ref job.PlanVersion);
                foreach (var point in Volatile.Read(ref job.Addresses))
                {
                    if (job.Cancellation.IsCancellationRequested) return;
                    if (point.Count is < 1 or > 125 || point.StartAddress < 0 ||
                        (long)point.StartAddress + point.Count - 1 > ushort.MaxValue) continue;
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation.Token);
                        timeout.CancelAfter(Math.Clamp(point.TimeoutMs, 50, 120_000));
                        var values = await _runtime.ReadModbusHoldingRegistersAsync(communicationId,
                            (ushort)point.StartAddress, (ushort)point.Count, timeout.Token).ConfigureAwait(false);
                        if (!_jobs.TryGetValue(communicationId, out var current) || !ReferenceEquals(current, job)) return;
                        if (planVersion != Volatile.Read(ref job.PlanVersion)) break;
                        var result = new ModbusPollResult(communicationId, point.AddressId, point.StartAddress,
                            point.Count, values.ToArray(), DateTimeOffset.Now);
                        _latest[(communicationId, point.AddressId)] = result;
                        pending.Add(result);
                    }
                    catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested) { return; }
                    catch (OperationCanceledException)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"轮询地址超时：设备={communicationId}, 地址块={point.AddressId}", "CommunicationPolling", target: LogTarget.File);
                    }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"轮询地址失败：设备={communicationId}, 地址块={point.AddressId}, 错误={ex.Message}",
                            "CommunicationPolling", ex, LogTarget.File);
                    }
                }
                if (pending.Count > 0 && planVersion == Volatile.Read(ref job.PlanVersion) &&
                    !job.Cancellation.IsCancellationRequested &&
                    _jobs.TryGetValue(communicationId, out var active) && ReferenceEquals(active, job))
                {
                    try { BatchReceived?.Invoke(this, new ModbusPollBatch(communicationId, job.Generation, planVersion, pending)); }
                    catch (Exception ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning,
                            $"轮询结果订阅者处理失败：设备={communicationId}, 错误={ex.Message}",
                            "CommunicationPolling", ex, LogTarget.File);
                    }
                }
                await Task.Delay(Volatile.Read(ref job.IntervalMs), job.Cancellation.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested) { }
        finally
        {
            if (_jobs.TryGetValue(communicationId, out var current) && ReferenceEquals(current, job))
                _jobs.TryRemove(communicationId, out _);
            job.Cancellation.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAllAsync().ConfigureAwait(false);
        _latest.Clear();
    }
}
