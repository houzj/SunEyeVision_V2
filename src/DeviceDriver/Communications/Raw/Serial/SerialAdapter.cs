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
    private sealed class SerialSession : ICommunicationAdapter
    {
        private readonly string _communicationId;
        private readonly SerialPort _port;
        private readonly ReceiveTerminatorFramer? _framer;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private Task? _receiveTask;
        private int _disposed;
        private int _receiving;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _receiving) == 1 && _port.IsOpen;

        public SerialSession(Communication communication)
        {
            _communicationId = communication.Id;
            var terminator = ReceiveTerminatorFramer.GetTerminatorBytes(communication);
            _framer = terminator == null ? null : new ReceiveTerminatorFramer(terminator);
            _port = new SerialPort(communication.SerialPort, communication.BaudRate,
                ParseParity(communication.Parity), communication.DataBits, ParseStopBits(communication.StopBits))
            {
                ReadTimeout = Math.Max(100, communication.CharacterTimeout),
                WriteTimeout = Math.Max(100, communication.RequestTimeout)
            };
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                cancellationToken.ThrowIfCancellationRequested();
                _port.Open();
                Volatile.Write(ref _receiving, 1);
                _receiveTask = Task.Run(() => ReceiveLoopAsync(_lifetime.Token), CancellationToken.None);
            }
            finally { _writeGate.Release(); }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var reason = "串口已关闭或接收任务已停止。";
            try
            {
                while (!cancellationToken.IsCancellationRequested && _port.IsOpen)
                {
                    try
                    {
                        var count = _port.Read(buffer, 0, buffer.Length);
                        if (count <= 0) continue;
                        var payload = new byte[count];
                        Buffer.BlockCopy(buffer, 0, payload, 0, count);
                        if (_framer == null)
                            DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, payload, DateTimeOffset.Now));
                        else
                            foreach (var message in _framer.Append(payload))
                                DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, message, DateTimeOffset.Now));
                    }
                    catch (TimeoutException) when (!cancellationToken.IsCancellationRequested) { }
                    catch (InvalidDataException ex)
                    {
                        VisionLogger.Instance.Log(LogLevel.Warning, $"串口接收组帧失败：设备={_communicationId}，{ex.Message}",
                            "CommunicationRuntime", ex, LogTarget.File);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                reason = ex.Message;
                VisionLogger.Instance.Log(LogLevel.Warning, $"串口接收停止：设备={_communicationId}, {ex.Message}",
                    "CommunicationRuntime", ex, LogTarget.File | LogTarget.UI);
            }
            finally
            {
                Volatile.Write(ref _receiving, 0);
                ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId, reason));
            }
            await Task.CompletedTask;
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!IsConnected) throw new IOException("串口尚未打开或接收任务已停止。");
                await Task.Run(() => _port.Write(payload.ToArray(), 0, payload.Length), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return 1;
            }
            finally { _writeGate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            await _writeGate.WaitAsync().ConfigureAwait(false);
            try { if (_port.IsOpen) _port.Close(); }
            finally { _writeGate.Release(); }
            if (_receiveTask is not null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _port.Dispose();
            _lifetime.Dispose();
        }
    }
}
