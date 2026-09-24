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

        await using var session = await OpenAsync(
            request.CameraRef,
            CameraAccessMode.Exclusive,
            new CameraConnectionOptions { Timeout = request.Timeout },
            cancellationToken).ConfigureAwait(false);

        return await session.Adapter.CaptureAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task CloseAsync(
        CameraDeviceRef cameraRef,
        CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryRemove(cameraRef.Value, out var session))
        {
            return;
        }

        try
        {
            await session.Adapter.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await session.DisposeAsync().ConfigureAwait(false);
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
