using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NModbus;
using NModbus.IO;
using S7.Net;
using InvalidDataException = System.IO.InvalidDataException;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Devices.Communication;
using Communication = SunEyeVision.DeviceDriver.Communications.CommunicationDeviceSnapshot;

namespace SunEyeVision.DeviceDriver.Communications;

public sealed partial class CommunicationRuntimeManager
{
    private sealed class UdpSession : ICommunicationAdapter
    {
        private readonly string _communicationId;
        private readonly UdpClient _client;
        private readonly string _remoteHost;
        private readonly int _remotePort;
        private readonly CancellationTokenSource _lifetime = new();
        private Task? _receiveTask;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => !_lifetime.IsCancellationRequested;

        public UdpSession(string communicationId, IPAddress address, int localPort, string remoteHost, int remotePort)
        {
            _communicationId = communicationId;
            _client = new UdpClient(new IPEndPoint(address, localPort));
            _remoteHost = remoteHost;
            _remotePort = remotePort;
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _receiveTask = ReceiveLoopAsync(_lifetime.Token);
            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var datagram = await _client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, datagram.Buffer, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                if (!_lifetime.IsCancellationRequested)
                    ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
            }
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(_remoteHost, cancellationToken).ConfigureAwait(false);
            addresses = Array.FindAll(addresses, address => address.AddressFamily == _client.Client.AddressFamily);
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            await _client.SendAsync(payload.ToArray(), new IPEndPoint(addresses[0], _remotePort), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            _client.Dispose();
            if (_receiveTask != null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (SocketException) { }
            }
            _lifetime.Dispose();
            ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
        }
    }
}

