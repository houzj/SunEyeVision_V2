using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Contracts.Acquisition;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

/// <summary>
/// 相机设备运行时的最小骨架。负责设备注册、适配器选择和会话独占，
/// 不负责 UI，也不向插件暴露厂商对象。
/// </summary>
public sealed class CameraRuntimeManager : ICameraFrameProvider, IAsyncDisposable
{
    private readonly IReadOnlyList<ICameraAdapterFactory> _factories;
    private readonly ConcurrentDictionary<string, CameraDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CameraSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _captureGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _connectionGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CaptureOwner> _captureOwners = new(StringComparer.OrdinalIgnoreCase);

    private readonly object _lifecycleGate=new();
    private bool _retiring, _retired;
    private int _active;
    private TaskCompletionSource<bool>? _drained;
    private Task? _disposeTask;

    private IDisposable EnterActivity()
    {
        lock(_lifecycleGate)
        {
            if(_retiring || _retired)throw new ObjectDisposedException(nameof(CameraRuntimeManager));
            checked{_active++;}
            return new CaptureLease(()=>
            {
                lock(_lifecycleGate)
                {
                    if(--_active==0)_drained?.TrySetResult(true);
                }
            });
        }
    }
    private sealed record CaptureOwner(Guid Token, string Name);

    /// <summary>协调调试预览与工作流采集；仅采集互斥，不改变设备连接所有权。</summary>
    public IDisposable AcquireCaptureLease(CameraDeviceRef cameraRef, string owner)
    {
        ArgumentNullException.ThrowIfNull(cameraRef);
        if (cameraRef.IsEmpty || string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("相机引用和采集用途不能为空。");
        var activity=EnterActivity();
        var lease = new CaptureOwner(Guid.NewGuid(), owner);
        try
        {
        while (!_captureOwners.TryAdd(cameraRef.Value, lease))
        {
            // The previous owner can release between TryAdd and the lookup.
            // Retry that transition instead of reporting a spurious missing key.
            if (_captureOwners.TryGetValue(cameraRef.Value, out var current))
                throw new InvalidOperationException($"相机 {cameraRef.Value} 正由{current.Name}采集，请先停止该采集。");
        }
        return new CaptureLease(() =>
        {
            try { if (_captureOwners.TryGetValue(cameraRef.Value, out var current) && current.Token == lease.Token) _captureOwners.TryRemove(cameraRef.Value, out _); }
            finally { activity.Dispose(); }
        });
        }
        catch { activity.Dispose(); throw; }
    }

    private sealed class CaptureLease(Action release) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                release();
        }
    }

    public CameraRuntimeManager(IEnumerable<ICameraAdapterFactory> factories)
    {
        ArgumentNullException.ThrowIfNull(factories);
        _factories = factories.ToArray();
    }

    public IReadOnlyCollection<CameraDescriptor> Devices => _descriptors.Values.ToArray();

    public bool TryGetSession(CameraDeviceRef cameraRef, out CameraSession session)
        => _sessions.TryGetValue(cameraRef.Value, out session!);

    /// <summary>Keep the exact connected session alive for an in-flight non-capture device operation.</summary>
    public IDisposable AcquireSessionOperationLease(CameraSession session, string purpose)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(purpose)) throw new ArgumentException("设备操作用途不能为空。", nameof(purpose));
        var activity=EnterActivity();
        try
        {
        if (!_sessions.TryGetValue(session.CameraRef.Value, out var before) || !ReferenceEquals(before, session))
            throw new InvalidOperationException("相机会话已关闭或重连。");
        var lease = session.AcquireOperation(purpose);
        if (_sessions.TryGetValue(session.CameraRef.Value, out var after) && ReferenceEquals(after, session))
            return new CaptureLease(()=>{try{lease.Dispose();}finally{activity.Dispose();}});
        lease.Dispose();
        throw new InvalidOperationException("相机会话已关闭或重连。");
        }
        catch{activity.Dispose();throw;}
    }

    public void RegisterDevice(CameraDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        using var activity=EnterActivity();
        if (string.IsNullOrWhiteSpace(descriptor.DeviceId) || descriptor.CameraRef.IsEmpty)
        {
            throw new ArgumentException("Camera descriptor must contain a stable device id and camera reference.", nameof(descriptor));
        }

        _descriptors[descriptor.CameraRef.Value] = descriptor;
    }

    public void RegisterDevices(IEnumerable<CameraDescriptor> descriptors)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        foreach (var descriptor in descriptors)
        {
            RegisterDevice(descriptor);
        }
    }

    public async Task<IReadOnlyList<CameraDescriptor>> DiscoverAsync(
        IEnumerable<ICameraDiscoveryProvider> providers,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providers);
        var tasks = providers.Select(provider => provider.DiscoverAsync(cancellationToken)).ToArray();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);
        var devices = results
            .SelectMany(result => result)
            .GroupBy(device => device.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();

        RegisterDevices(devices);
        return devices;
    }

    public async Task<CameraSession> OpenAsync(
        CameraDeviceRef cameraRef,
        CameraAccessMode accessMode,
        CameraConnectionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cameraRef);
        using var activity=EnterActivity();
        var gate = _connectionGates.GetOrAdd(cameraRef.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_descriptors.TryGetValue(cameraRef.Value, out var descriptor))
                throw new InvalidOperationException($"Camera '{cameraRef.Value}' is not registered.");
            if (_sessions.ContainsKey(cameraRef.Value))
                throw new InvalidOperationException($"Camera '{cameraRef.Value}' is already in use.");
            var factory = _factories.FirstOrDefault(item => item.CanCreate(descriptor));
            if (factory is null)
                throw new InvalidOperationException($"No camera adapter is available for '{cameraRef.Value}'.");
            var adapter = factory.Create(descriptor);
            var session = new CameraSession(cameraRef, accessMode, adapter,
                requested => new ValueTask(CloseSessionAsync(requested, CancellationToken.None)),
                disposed =>
                {
                    if (_sessions.TryGetValue(cameraRef.Value, out var current) && ReferenceEquals(current, disposed))
                        _sessions.TryRemove(cameraRef.Value, out _);
                    return Task.CompletedTask;
                });
            try
            {
                await adapter.ConnectAsync(options ?? new CameraConnectionOptions(), cancellationToken).ConfigureAwait(false);
                if (!_sessions.TryAdd(cameraRef.Value, session))
                    throw new InvalidOperationException($"Camera '{cameraRef.Value}' is already in use.");
                return session;
            }
            catch (Exception failure)
            {
                try { await session.DisposeCoreAsync().ConfigureAwait(false); }
                catch (Exception cleanup) { throw new AggregateException("相机连接及清理均失败。", failure, cleanup); }
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public ValueTask<ICameraFrameLease> CaptureAsync(CameraFrameRequest request,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<ICameraFrameLease>(CaptureTrackedAsync(request,cancellationToken));
    }
    private async Task<ICameraFrameLease> CaptureTrackedAsync(CameraFrameRequest request,CancellationToken cancellationToken)
    {
        using var activity=EnterActivity();
        if(request.Purpose == CameraRequestPurpose.Workflow)
        {
            if(!_descriptors.TryGetValue(request.DeviceId,out var descriptor)) throw new InvalidOperationException($"相机 {request.DeviceId} 未发现，请重新选择设备。");
            var factory=_factories.FirstOrDefault(item=>item.CanCreate(descriptor));
            if(factory is null || !factory.SupportsWorkflowFrames(descriptor)) throw new InvalidOperationException("相机采集有效域尚未确认，不能用于工作流。");
        }
        return await CaptureSingleFrameAsync(new CameraCaptureRequest {
            CameraRef=new CameraDeviceRef(request.DeviceId),ProducerRun=request.ProducerRun,Purpose=request.Purpose,
            Timeout=request.Timeout,DiagnosticCorrelationId=request.DiagnosticCorrelationId
        },cancellationToken).ConfigureAwait(false);
    }
    private async Task<ICameraFrameLease> CaptureSingleFrameAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var gate = _captureGates.GetOrAdd(request.CameraRef.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Single-frame acquisition shares the same exclusivity as preview and workflow streams.
            // Keep the existing gate so concurrent single-frame requests still serialize.
            using var lease = AcquireCaptureLease(request.CameraRef, "单帧采集");
            // 已由设备列表连接的会话归应用所有；本次取帧绝不能关闭它。
            if (_sessions.TryGetValue(request.CameraRef.Value, out var existing))
                return await CaptureConfiguredFrameAsync(existing, request, cancellationToken).ConfigureAwait(false);

            var temporary = await OpenAsync(
                request.CameraRef,
                CameraAccessMode.Exclusive,
                new CameraConnectionOptions { Timeout = request.Timeout },
                cancellationToken).ConfigureAwait(false);
            ICameraFrameLease? frame = null;
            Exception? failure = null;
            try { frame = await CaptureConfiguredFrameAsync(temporary, request, cancellationToken).ConfigureAwait(false); }
            catch (Exception error) { failure = error; }
            try { await CloseConnectedSessionAsync(temporary.CameraRef, temporary, CancellationToken.None, waitForOperations: true).ConfigureAwait(false); }
            catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
            if (failure is not null)
            {
                try { if(frame is not null)await frame.DisposeAsync().ConfigureAwait(false); }
                catch (Exception error) { failure = new AggregateException(failure, error); }
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            }
            return frame!;
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<ICameraFrameLease> CaptureConfiguredFrameAsync(
        CameraSession session, CameraCaptureRequest request, CancellationToken cancellationToken)
    {
        var configuration = await session.Adapter.ReadCaptureConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!configuration.IsValid)
            throw new InvalidOperationException(configuration.StatusMessage ?? "无法读取相机触发配置");

        var parameters = new Dictionary<string, object?>(request.Parameters)
        {
            // 仅工作流单帧请求自动执行一次软件触发；预览流不带此标记。
            ["FireSoftwareTrigger"] = request.Purpose == CameraRequestPurpose.Workflow && configuration.StreamMode == CameraTriggerMode.Software
        };
        var configuredRequest = request with
        {
            TriggerMode = configuration.StreamMode,
            Parameters = parameters
        };
        return await session.Adapter.CaptureAsync(configuredRequest, cancellationToken).ConfigureAwait(false);
    }

    public Task CloseAsync(CameraDeviceRef cameraRef, CancellationToken cancellationToken = default)
        => CloseWithCaptureGuardAsync(cameraRef, null, cancellationToken);

    private Task CloseSessionAsync(CameraSession session, CancellationToken cancellationToken)
        => CloseWithCaptureGuardAsync(session.CameraRef, session, cancellationToken);

    private async Task CloseWithCaptureGuardAsync(CameraDeviceRef cameraRef, CameraSession? expected,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cameraRef);
        if (expected?.CloseResult is { IsFaulted: false } existingClose)
        { await existingClose.ConfigureAwait(false); return; }
        if (expected is not null && (!_sessions.TryGetValue(cameraRef.Value, out var current)
            || !ReferenceEquals(expected, current))) return;
        var gate = _captureGates.GetOrAdd(cameraRef.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (expected?.CloseResult is { IsFaulted: false } completedClose)
            { await completedClose.ConfigureAwait(false); return; }
            if (expected is not null && (!_sessions.TryGetValue(cameraRef.Value, out var currentAfterGate)
                || !ReferenceEquals(expected, currentAfterGate))) return;
            // Hold the same ownership token as all producers before removing a session.
            using var lease = AcquireCaptureLease(cameraRef, "设备关闭");
            await CloseConnectedSessionAsync(cameraRef, expected, cancellationToken).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    private async Task CloseConnectedSessionAsync(CameraDeviceRef cameraRef, CameraSession? expected,
        CancellationToken cancellationToken, bool waitForOperations = false)
    {
        var gate = _connectionGates.GetOrAdd(cameraRef.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_sessions.TryGetValue(cameraRef.Value, out var session)
                || (expected is not null && !ReferenceEquals(expected, session))) return;
            var (completion, operationsDrained) = session.BeginClose(waitForOperations);
            await operationsDrained.ConfigureAwait(false);
            Exception? failure = null;
            try { await session.Adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception error) { failure = error; }
            try { await session.DisposeCoreAsync().ConfigureAwait(false); }
            catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
            if (failure is null) completion.TrySetResult(true);
            else completion.TrySetException(failure);
            await completion.Task.ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        Task task;
        lock(_lifecycleGate)
        {
            if(_retired)return;
            _retiring=true;
            task=_disposeTask??=DisposeCoreAsync();
        }
        try { await task.ConfigureAwait(false); }
        catch
        {
            lock(_lifecycleGate) if(ReferenceEquals(_disposeTask,task))_disposeTask=null;
            throw;
        }
    }
    private async Task DisposeCoreAsync()
    {
        Task pending;
        lock(_lifecycleGate)
            pending=_active==0 ? Task.CompletedTask : (_drained??=new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        await pending.ConfigureAwait(false);
        var failures=new List<Exception>();
        foreach(var cameraRef in _sessions.Keys.ToArray())
        {
            try { await CloseConnectedSessionAsync(new CameraDeviceRef(cameraRef),null,CancellationToken.None,waitForOperations:true).ConfigureAwait(false); }
            catch(Exception error) { failures.Add(error); }
        }
        if(failures.Count!=0)throw new AggregateException("相机管理器关闭失败。",failures);
        lock(_lifecycleGate)_retired=true;
    }
}