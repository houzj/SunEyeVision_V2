using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Devices.Camera;

namespace SunEyeVision.DeviceDriver.Cameras.Contracts;

/// <summary>
/// 相机设备运行时的最小骨架。负责设备注册、适配器选择和会话独占，
/// 不负责 UI，也不向插件暴露厂商对象。
/// </summary>
public sealed class CameraRuntimeManager : ICameraCaptureService, IAsyncDisposable
{
    private readonly IReadOnlyList<ICameraAdapterFactory> _factories;
    private readonly ConcurrentDictionary<string, CameraDescriptor> _descriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CameraSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _captureGates = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CaptureOwner> _captureOwners = new(StringComparer.OrdinalIgnoreCase);

    private sealed record CaptureOwner(Guid Token, string Name);

    /// <summary>协调调试预览与工作流采集；仅采集互斥，不改变设备连接所有权。</summary>
    public IDisposable AcquireCaptureLease(CameraDeviceRef cameraRef, string owner)
    {
        ArgumentNullException.ThrowIfNull(cameraRef);
        if (cameraRef.IsEmpty || string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("相机引用和采集用途不能为空。");
        var lease = new CaptureOwner(Guid.NewGuid(), owner);
        if (!_captureOwners.TryAdd(cameraRef.Value, lease))
        {
            var current = _captureOwners[cameraRef.Value];
            throw new InvalidOperationException($"相机 {cameraRef.Value} 正由{current.Name}采集，请先停止该采集。");
        }
        return new CaptureLease(() =>
        {
            if (_captureOwners.TryGetValue(cameraRef.Value, out var current)
                && current.Token == lease.Token)
                _captureOwners.TryRemove(cameraRef.Value, out _);
        });
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

    public void RegisterDevice(CameraDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

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

        if (!_descriptors.TryGetValue(cameraRef.Value, out var descriptor))
        {
            throw new InvalidOperationException($"Camera '{cameraRef.Value}' is not registered.");
        }

        if (_sessions.ContainsKey(cameraRef.Value))
        {
            throw new InvalidOperationException($"Camera '{cameraRef.Value}' is already in use.");
        }

        var factory = _factories.FirstOrDefault(item => item.CanCreate(descriptor));
        if (factory == null)
        {
            throw new InvalidOperationException($"No camera adapter is available for '{cameraRef.Value}'.");
        }

        var adapter = factory.Create(descriptor);
        CameraSession? session = null;
        session = new CameraSession(
            cameraRef,
            accessMode,
            adapter,
            disposedSession =>
            {
                if (_sessions.TryGetValue(cameraRef.Value, out var current)
                    && ReferenceEquals(current, disposedSession))
                {
                    _sessions.TryRemove(cameraRef.Value, out _);
                }
                return Task.CompletedTask;
            });
        try
        {
            await adapter.ConnectAsync(options ?? new CameraConnectionOptions(), cancellationToken)
                .ConfigureAwait(false);
            if (!_sessions.TryAdd(cameraRef.Value, session))
            {
                await session.DisposeAsync().ConfigureAwait(false);
                throw new InvalidOperationException($"Camera '{cameraRef.Value}' is already in use.");
            }

            return session;
        }
        catch
        {
            await adapter.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<CameraFrame> CaptureAsync(
        CameraCaptureRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var gate = _captureGates.GetOrAdd(request.CameraRef.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // 已由设备列表连接的会话归应用所有；本次取帧绝不能关闭它。
            if (_sessions.TryGetValue(request.CameraRef.Value, out var existing))
                return await CaptureConfiguredFrameAsync(existing, request, cancellationToken).ConfigureAwait(false);

            await using var temporary = await OpenAsync(
                request.CameraRef,
                CameraAccessMode.Exclusive,
                new CameraConnectionOptions { Timeout = request.Timeout },
                cancellationToken).ConfigureAwait(false);
            return await CaptureConfiguredFrameAsync(temporary, request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private static async Task<CameraFrame> CaptureConfiguredFrameAsync(
        CameraSession session, CameraCaptureRequest request, CancellationToken cancellationToken)
    {
        var configuration = await session.Adapter.ReadCaptureConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!configuration.IsValid)
            throw new InvalidOperationException(configuration.StatusMessage ?? "无法读取相机触发配置");

        var parameters = new Dictionary<string, object?>(request.Parameters)
        {
            // 仅工作流单帧请求自动执行一次软件触发；预览流不带此标记。
            ["FireSoftwareTrigger"] = configuration.StreamMode == CameraTriggerMode.Software
        };
        var configuredRequest = request with
        {
            TriggerMode = configuration.StreamMode,
            Parameters = parameters
        };
        return await session.Adapter.CaptureAsync(configuredRequest, cancellationToken).ConfigureAwait(false);
    }

    public async Task CloseAsync(
        CameraDeviceRef cameraRef,
        CancellationToken cancellationToken = default)
    {
        var gate = _captureGates.GetOrAdd(cameraRef.Value, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_sessions.TryRemove(cameraRef.Value, out var session))
                return;
            try
            {
                await session.Adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var cameraRef in _sessions.Keys.ToArray())
        {
            await CloseAsync(new CameraDeviceRef(cameraRef)).ConfigureAwait(false);
        }
    }
}
