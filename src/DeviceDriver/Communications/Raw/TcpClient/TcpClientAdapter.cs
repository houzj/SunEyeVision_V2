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
    private sealed class TcpClientSession : ICommunicationAdapter
    {
        private readonly string _communicationId;
        private readonly string _host;
        private readonly int _port;
        private readonly TimeSpan _connectTimeout;
        private readonly ReceiveTerminatorFramer? _framer;
        private readonly TcpClient _client = new();
        private readonly CancellationTokenSource _lifetime = new();
        private NetworkStream? _stream;
        private Task? _receiveTask;
        private int _disposed;
        private int _connected;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => Volatile.Read(ref _connected) == 1;

        public TcpClientSession(string communicationId, string host, int port, TimeSpan connectTimeout,
            byte[]? receiveTerminator)
        {
            _communicationId = communicationId;
            _host = host;
            _port = port;
            _connectTimeout = connectTimeout;
            _framer = receiveTerminator == null ? null : new ReceiveTerminatorFramer(receiveTerminator);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            timeout.CancelAfter(_connectTimeout);
            await _client.ConnectAsync(_host, _port, timeout.Token).ConfigureAwait(false);
            _stream = _client.GetStream();
            Volatile.Write(ref _connected, 1);
            _receiveTask = ReceiveLoopAsync(_stream, _lifetime.Token);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var stream = _stream ?? throw new InvalidOperationException("TCP 连接尚未建立。");
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return 1;
        }

        private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (count == 0) break;
                    var data = new byte[count];
                    Buffer.BlockCopy(buffer, 0, data, 0, count);
                    if (_framer == null)
                        DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, data, DateTimeOffset.Now));
                    else
                        foreach (var message in _framer.Append(data))
                            DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, message, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            catch (InvalidDataException ex)
            {
                VisionLogger.Instance.Log(LogLevel.Warning, $"TCP 客户端接收组帧失败：设备={_communicationId}，{ex.Message}",
                    "CommunicationRuntime", ex, LogTarget.File);
            }
            catch (IOException) { }
            catch (SocketException) { }
            finally
            {
                Volatile.Write(ref _connected, 0);
                ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
                _stream?.Dispose();
                _client.Dispose();
                _lifetime.Cancel();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            Volatile.Write(ref _connected, 0);
            _client.Close();
            if (_receiveTask is not null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _stream?.Dispose();
            _lifetime.Dispose();
            _client.Dispose();
        }
    }
}
