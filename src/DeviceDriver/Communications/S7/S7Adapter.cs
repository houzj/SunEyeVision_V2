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
    private interface IS7Session
    {
        Task<byte[]> ReadBytesAsync(DataType area, int dataBlock, int startByte, int count, CancellationToken cancellationToken);
        Task WriteBytesAsync(DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value, CancellationToken cancellationToken);
    }

    private sealed class S7Session : ICommunicationAdapter, IS7Session
    {
        private readonly Communication _communication;
        private readonly SemaphoreSlim _operationGate = new(1, 1);
        private Plc? _plc;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _plc?.IsConnected == true;

        public S7Session(Communication communication) { _communication = communication; }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<CpuType>(_communication.S7CpuType, true, out var cpuType) || !Enum.IsDefined(cpuType))
                throw new ArgumentException($"不支持的 S7 CPU 类型：{_communication.S7CpuType}。");
            var plc = new Plc(cpuType, _communication.Host, _communication.Port, (short)_communication.Rack, (short)_communication.Slot);
            try { await plc.OpenAsync(cancellationToken).ConfigureAwait(false); _plc = plc; }
            catch { plc.Close(); throw; }
        }

        public async Task<byte[]> ReadBytesAsync(DataType area, int dataBlock, int startByte, int count, CancellationToken cancellationToken)
        {
            if (count is < 1 or > 2000) throw new ArgumentOutOfRangeException(nameof(count), "单次 S7 读取长度必须为 1 到 2000 字节。");
            if (dataBlock < 0 || startByte < 0) throw new ArgumentOutOfRangeException(nameof(startByte), "数据块与起始字节不能为负数。");
            var plc = _plc ?? throw new InvalidOperationException("S7 会话未连接。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await plc.ReadBytesAsync(area, dataBlock, startByte, count, cancellationToken).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        public async Task WriteBytesAsync(DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
        {
            if (value.IsEmpty || value.Length > 2000) throw new ArgumentOutOfRangeException(nameof(value), "单次 S7 写入长度必须为 1 到 2000 字节。");
            if (dataBlock < 0 || startByte < 0) throw new ArgumentOutOfRangeException(nameof(startByte), "数据块与起始字节不能为负数。");
            var plc = _plc ?? throw new InvalidOperationException("S7 会话未连接。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await plc.WriteBytesAsync(area, dataBlock, startByte, value, cancellationToken).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        public Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException("S7 是类型化 PLC 协议，请使用 S7 数据区读写操作，不支持原始字节发送。");

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) { _plc?.Close(); _plc = null; _operationGate.Dispose(); }
            return ValueTask.CompletedTask;
        }
    }
}

