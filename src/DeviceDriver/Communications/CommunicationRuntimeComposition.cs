using System;
using System.Threading.Tasks;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>Application-owned communication runtime, independent of management windows.</summary>
public sealed class CommunicationRuntimeComposition : IAsyncDisposable
{
    public CommunicationRuntimeManager Runtime { get; }
    public CommunicationPollingCoordinator Polling { get; }

    public CommunicationRuntimeComposition(string? opcUaCertificateStoreRoot = null)
    {
        Runtime = new CommunicationRuntimeManager(opcUaCertificateStoreRoot);
        Polling = new CommunicationPollingCoordinator(Runtime);
        Runtime.ConnectionClosed += OnConnectionClosed;
    }

    private void OnConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e) =>
        Polling.Stop(e.CommunicationId);

    public async ValueTask DisposeAsync()
    {
        Runtime.ConnectionClosed -= OnConnectionClosed;
        await Polling.DisposeAsync().ConfigureAwait(false);
        await Runtime.DisposeAsync().ConfigureAwait(false);
    }
}
