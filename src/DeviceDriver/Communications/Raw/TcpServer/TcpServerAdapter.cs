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
    private sealed class TcpServerSession : ICommunicationAdapter
    {
        private readonly string _communicationId;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
        private readonly ConcurrentDictionary<int, Task> _receiveTasks = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private Task? _acceptTask;
        private int _clientSequence;
        private int _running;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => Volatile.Read(ref _running) == 1;

        public TcpServerSession(string communicationId, IPAddress address, int port)
        {
            _communicationId = communicationId;
            _listener = new TcpListener(address, port);
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _listener.Start();
            Volatile.Write(ref _running, 1);
            _acceptTask = AcceptLoopAsync(_lifetime.Token);
            return Task.CompletedTask;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    var id = Interlocked.Increment(ref _clientSequence);
                    _clients[id] = client;
                    _receiveTasks[id] = ReceiveClientAsync(id, client, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                Volatile.Write(ref _running, 0);
                if (!_lifetime.IsCancellationRequested)
                    ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
            }
        }

        private async Task ReceiveClientAsync(int id, TcpClient client, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                var stream = client.GetStream();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (count == 0) break;
                    var data = new byte[count];
                    Buffer.BlockCopy(buffer, 0, data, 0, count);
                    DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, data, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            finally
            {
                _clients.TryRemove(id, out _);
                client.Dispose();
            }
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var clients = _clients.ToArray();
            if (clients.Length == 0)
                throw new InvalidOperationException("TCP 服务端尚无已连接客户端，暂不能发送。");

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var delivered = 0;
                foreach (var (id, client) in clients)
                {
                    try
                    {
                        await client.GetStream().WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                        delivered++;
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
                    {
                        _clients.TryRemove(id, out _);
                        client.Dispose();
                    }
                }

                if (delivered == 0)
                    throw new IOException("发送失败：所有 TCP 客户端连接均已断开。");

                return delivered;
            }
            finally { _sendLock.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Volatile.Write(ref _running, 0);
            _lifetime.Cancel();
            _listener.Stop();
            foreach (var client in _clients.Values) client.Dispose();
            if (_acceptTask != null)
            {
                try { await _acceptTask.ConfigureAwait(false); }
                catch (SocketException) { }
            }
            try { await Task.WhenAll(_receiveTasks.Values).ConfigureAwait(false); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            _sendLock.Dispose();
            _lifetime.Dispose();
            ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
        }
    }
}
