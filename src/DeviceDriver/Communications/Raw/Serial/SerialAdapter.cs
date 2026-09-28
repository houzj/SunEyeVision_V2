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
        private readonly CancellationTokenSource _lifetime = new();
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private Task? _receiveTask;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _port.IsOpen;

        public SerialSession(Communication communication)
        {
            _communicationId = communication.Id;
            _port = new SerialPort(communication.SerialPort, communication.BaudRate,
                ParseParity(communication.Parity), communication.DataBits, ParseStopBits(communication.StopBits))
            {
                ReadTimeout = Math.Max(100, communication.CharacterTimeout),
                WriteTimeout = Math.Max(100, communication.RequestTimeout)
            };
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _port.Open();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_lifetime.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
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
                        DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, payload, DateTimeOffset.Now));
                    }
                    catch (TimeoutException) when (!cancellationToken.IsCancellationRequested) { }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            finally
            {
                ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
            }
            await Task.CompletedTask;
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            if (!_port.IsOpen) throw new InvalidOperationException("串口尚未打开。");
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => _port.Write(payload.ToArray(), 0, payload.Length), cancellationToken).ConfigureAwait(false);
                return 1;
            }
            finally { _writeGate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            if (_port.IsOpen) _port.Close();
            if (_receiveTask is not null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _port.Dispose();
            _writeGate.Dispose();
            _lifetime.Dispose();
        }
    }
}

