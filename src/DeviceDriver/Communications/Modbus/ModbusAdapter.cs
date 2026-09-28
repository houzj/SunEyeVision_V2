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
    private sealed class ModbusSession : ICommunicationAdapter, IModbusOperations
    {
        private readonly Communication _communication;
        private readonly SemaphoreSlim _operationGate = new(1, 1);
        private TcpClient? _tcpClient;
        private SerialPort? _serialPort;
        private PortStreamResource? _serialResource;
        private IModbusMaster? _master;
        private int _disposed;
        private long _operationSequence;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _master != null && (_tcpClient?.Connected ?? _serialPort?.IsOpen ?? false);

        public ModbusSession(Communication communication) => _communication = communication;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var factory = new ModbusFactory();
            ModbusDiagnostic(LogLevel.Info, $"Modbus会话连接：设备={_communication.Name}({_communication.Id}), 类型={(_communication.IsModbusRtu ? "RTU" : "TCP")}, 站号={_communication.UnitId}, 请求超时={GetTimeout(_communication).TotalMilliseconds:0}ms");
            if (!_communication.IsModbusRtu)
            {
                _tcpClient = new TcpClient();
                using var timeout = new CancellationTokenSource(GetTimeout(_communication));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
                await _tcpClient.ConnectAsync(_communication.Host, _communication.Port, linked.Token).ConfigureAwait(false);
                _master = factory.CreateMaster(_tcpClient);
            }
            else
            {
                _serialPort = new SerialPort(_communication.SerialPort, _communication.BaudRate,
                    ParseParity(_communication.Parity), _communication.DataBits, ParseStopBits(_communication.StopBits))
                {
                    ReadTimeout = (int)GetTimeout(_communication).TotalMilliseconds,
                    WriteTimeout = (int)GetTimeout(_communication).TotalMilliseconds
                };
                _serialPort.Open();
                _serialResource = new PortStreamResource(_serialPort);
                _master = factory.CreateRtuMaster(_serialResource);
            }
            _master.Transport.ReadTimeout = (int)GetTimeout(_communication).TotalMilliseconds;
            _master.Transport.WriteTimeout = (int)GetTimeout(_communication).TotalMilliseconds;
            _master.Transport.Retries = 0;
            ModbusDiagnostic(LogLevel.Info, $"Modbus会话已就绪：设备={_communication.Name}({_communication.Id}), 传输读超时={_master.Transport.ReadTimeout}ms, 写超时={_master.Transport.WriteTimeout}ms, 重试={_master.Transport.Retries}, TCP已连接={_tcpClient?.Connected}, 串口已打开={_serialPort?.IsOpen}");
        }

        public Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Modbus 必须通过功能码进行类型化读写，不能发送任意原始数据。");

        public Task<bool[]> ReadCoilsAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadCoilsAsync(GetUnitId(), address, count), cancellationToken);

        public Task<bool[]> ReadInputsAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadInputsAsync(GetUnitId(), address, count), cancellationToken);

        public async Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken)
        {
            var operationId = Interlocked.Increment(ref _operationSequence);
            var started = System.Diagnostics.Stopwatch.GetTimestamp();
            var unitId = GetUnitId();
            ModbusDiagnostic(LogLevel.Info, $"FC03请求开始：设备={_communication.Name}({_communication.Id}), 操作={operationId}, 站号={unitId}, 地址={address}, 数量={count}, 取消状态={cancellationToken.IsCancellationRequested}");
            try
            {
                var values = await ExecuteAsync(master => master.ReadHoldingRegistersAsync(unitId, address, count), cancellationToken).ConfigureAwait(false);
                var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                ModbusDiagnostic(LogLevel.Info, $"FC03请求完成：设备={_communication.Name}({_communication.Id}), 操作={operationId}, 耗时={elapsedMs:0.0}ms, 返回数量={values.Length}, 返回值={string.Join(",", values.Select(value => $"0x{value:X4}"))}");
                return values;
            }
            catch (Exception ex)
            {
                var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                ModbusDiagnostic(ex is OperationCanceledException ? LogLevel.Warning : LogLevel.Error,
                    $"FC03请求失败：设备={_communication.Name}({_communication.Id}), 操作={operationId}, 耗时={elapsedMs:0.0}ms, 地址={address}, 数量={count}, 错误类型={ex.GetType().Name}, 错误={ex.Message}", ex);
                throw;
            }
        }

        public Task<ushort[]> ReadInputRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadInputRegistersAsync(GetUnitId(), address, count), cancellationToken);

        public Task WriteCoilAsync(ushort address, bool value, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.WriteSingleCoilAsync(GetUnitId(), address, value), cancellationToken);

        public Task WriteRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.WriteSingleRegisterAsync(GetUnitId(), address, value), cancellationToken);

        public Task WriteCoilsAsync(ushort address, bool[] values, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length is < 1 or > 1968) throw new ArgumentOutOfRangeException(nameof(values), "FC15 一次写入数量必须为 1 到 1968 个线圈。");
            if (address > ushort.MaxValue - values.Length + 1) throw new ArgumentOutOfRangeException(nameof(address), "线圈地址范围超出 Modbus 地址空间。");
            return ExecuteAsync(master => master.WriteMultipleCoilsAsync(GetUnitId(), address, values), cancellationToken);
        }

        public Task WriteRegistersAsync(ushort address, ushort[] values, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length is < 1 or > 123) throw new ArgumentOutOfRangeException(nameof(values), "FC16 一次写入数量必须为 1 到 123 个寄存器。");
            if (address > ushort.MaxValue - values.Length + 1) throw new ArgumentOutOfRangeException(nameof(address), "寄存器地址范围超出 Modbus 地址空间。");
            return ExecuteAsync(master => master.WriteMultipleRegistersAsync(GetUnitId(), address, values), cancellationToken);
        }

        private async Task<T> ExecuteAsync<T>(Func<IModbusMaster, Task<T>> operation, CancellationToken cancellationToken)
        {
            var master = _master ?? throw new InvalidOperationException("Modbus 会话尚未建立。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            // NModbus does not accept a cancellation token. Do not release the gate while its
            // underlying I/O is still running: a later request would corrupt the same stream.
            try { return await operation(master).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        private async Task ExecuteAsync(Func<IModbusMaster, Task> operation, CancellationToken cancellationToken)
        {
            var master = _master ?? throw new InvalidOperationException("Modbus 会话尚未建立。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await operation(master).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        private byte GetUnitId() => _communication.UnitId is >= 0 and <= 255
            ? (byte)_communication.UnitId
            : throw new ArgumentOutOfRangeException(nameof(_communication.UnitId), "Modbus 站号必须在 0 到 255 之间。");

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
            _master?.Dispose();
            _serialResource?.Dispose();
            _serialPort?.Dispose();
            _tcpClient?.Dispose();
            _operationGate.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PortStreamResource : IStreamResource, IDisposable
    {
        private readonly SerialPort _port;
        public int InfiniteTimeout => Timeout.Infinite;
        public int ReadTimeout { get => _port.ReadTimeout; set => _port.ReadTimeout = value; }
        public int WriteTimeout { get => _port.WriteTimeout; set => _port.WriteTimeout = value; }

        public PortStreamResource(SerialPort port) => _port = port;
        public void DiscardInBuffer() => _port.DiscardInBuffer();
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
        public void Dispose() { }
    }

    private static Parity ParseParity(string value) => value switch
    {
        "奇校验" or "Odd" or "ODD PARITY" => Parity.Odd,
        "偶校验" or "Even" or "EVEN PARITY" => Parity.Even,
        "标记校验" or "Mark" or "MARK PARITY" => Parity.Mark,
        "空格校验" or "Space" or "SPACE PARITY" => Parity.Space,
        _ => Parity.None
    };

    private static StopBits ParseStopBits(int value) => value switch
    {
        1 => StopBits.One,
        2 => StopBits.Two,
        _ => throw new ArgumentOutOfRangeException(nameof(value), "停止位必须为 1 或 2。")
    };
}
