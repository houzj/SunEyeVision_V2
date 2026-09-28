using System;
using System.Threading;
using System.Threading.Tasks;

namespace SunEyeVision.DeviceDriver.Communications;

/// <summary>Driver-owned connection boundary. Protocol-specific operations use separate typed interfaces.</summary>
internal interface ICommunicationAdapter : IAsyncDisposable
{
    bool IsConnected { get; }
    event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
    event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
    Task ConnectAsync(CancellationToken cancellationToken);
    Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
}
