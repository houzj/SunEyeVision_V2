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
        private readonly byte[]? _receiveTerminator;
        private readonly TimeSpan _requestTimeout;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
        private readonly ConcurrentDictionary<int, Task> _receiveTasks = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly object _lifecycleGate = new();
        private Task? _acceptTask;
        private int _clientSequence;
        private int _running;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _running) == 1;

        public TcpServerSession(string communicationId, IPAddress address, int port, byte[]? receiveTerminator, TimeSpan requestTimeout)
        {
            _communicationId = communicationId;
            _listener = new TcpListener(address, port);
            _receiveTerminator = receiveTerminator;
            _requestTimeout = requestTimeout;
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            lock (_lifecycleGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                _listener.Start();
                Volatile.Write(ref _running, 1);
                _acceptTask = AcceptLoopAsync(_lifetime.Token);
            }
            return Task.CompletedTask;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            var reason = "TCP 监听已停止。";
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    var id = Interlocked.Increment(ref _clientSequence);
                    _clients[id] = client;
                    var receive = ReceiveClientAsync(id, client, cancellationToken);
                    _receiveTasks[id] = receive;
                    _ = receive.ContinueWith(completed =>
                    {
                        _receiveTasks.TryRemove(id, out _);
                        if (completed.Exception is { } error)
                            VisionLogger.Instance.Log(LogLevel.Warning,
                                $"TCP客户端接收任务失败：设备={_communicationId}, 客户端={id}, {error.GetBaseException().Message}",
                                "CommunicationRuntime", error, LogTarget.File);
                    }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException ex) { reason = ex.Message; }
            finally
            {
                Volatile.Write(ref _running, 0);
                if (!_lifetime.IsCancellationRequested)
                    ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId, reason));
            }
        }

        private async Task ReceiveClientAsync(int id, TcpClient client, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            var framer = _receiveTerminator == null ? null : new ReceiveTerminatorFramer(_receiveTerminator);
            try
            {
                var stream = client.GetStream();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (count == 0) break;
                    var data = new byte[count];
                    Buffer.BlockCopy(buffer, 0, data, 0, count);
                    if (framer == null)
                        DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, data, DateTimeOffset.Now));
                    else
                        foreach (var message in framer.Append(data))
                            DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, message, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (InvalidDataException ex)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, $"TCP 服务端接收组帧失败：设备={_communicationId}，客户端={id}，{ex.Message}",
                    "CommunicationRuntime", ex, LogTarget.File);
            }
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

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            timeout.CancelAfter(_requestTimeout);
            await _sendLock.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                var delivered = 0;
                foreach (var (id, client) in clients)
                {
                    try
                    {
                        await client.GetStream().WriteAsync(payload, timeout.Token).ConfigureAwait(false);
                        timeout.Token.ThrowIfCancellationRequested();
                        delivered++;
                    }
                    catch (OperationCanceledException)
                    {
                        _clients.TryRemove(id, out _);
                        client.Dispose();
                        throw;
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
            lock (_lifecycleGate)
            {
                Volatile.Write(ref _running, 0);
                _lifetime.Cancel();
                _listener.Stop();
            }
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
            _lifetime.Dispose();
            ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
        }
    }
}
