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

/// <summary>当前进程内的通讯连接生命周期管理。协议驱动不依赖 WPF/UI。</summary>
public sealed partial class CommunicationRuntimeManager : ICommunicationStatusService, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ICommunicationAdapter> _sessions = new();
    private readonly IReadOnlyDictionary<string, ICommunicationProtocolFactory> _factories;
    private readonly ConcurrentDictionary<string, CommunicationStatusSnapshot> _statuses = new();
    private readonly string _opcUaCertificateStoreRoot;

    private static void ModbusDiagnostic(LogLevel level, string message, Exception? exception = null) =>
        VisionLogger.Instance.Log(level, $"[Modbus诊断] {message}", "CommunicationRuntime", exception,
            LogTarget.File | LogTarget.UI);

    public CommunicationRuntimeManager(string? opcUaCertificateStoreRoot = null)
    {
        _opcUaCertificateStoreRoot = string.IsNullOrWhiteSpace(opcUaCertificateStoreRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunEyeVision", "OPC Foundation", "CertificateStores")
            : Path.GetFullPath(opcUaCertificateStoreRoot);
        _factories = BuildFactoryRegistry(CreateDefaultFactories(_opcUaCertificateStoreRoot));
    }

    public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
    public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
    public event EventHandler<CommunicationStatusSnapshot>? StatusChanged;

    public CommunicationStatusSnapshot GetStatus(CommunicationDeviceRef device)
    {
        ArgumentNullException.ThrowIfNull(device);
        return _statuses.TryGetValue(device.Value, out var status)
            ? status
            : new CommunicationStatusSnapshot(device, CommunicationState.Disconnected,
                CommunicationCapabilities.None, null, DateTimeOffset.UtcNow);
    }

    private void PublishStatus(CommunicationDeviceRef device, CommunicationState state,
        CommunicationCapabilities capabilities, CommunicationError? error = null)
    {
        var status = new CommunicationStatusSnapshot(device, state, capabilities, error, DateTimeOffset.UtcNow);
        _statuses[device.Value] = status;
        try { StatusChanged?.Invoke(this, status); }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning, $"通讯状态订阅者失败：{ex.Message}",
                "CommunicationRuntime", ex, LogTarget.File);
        }
    }

    private CommunicationCapabilities GetCapabilities(Communication communication) =>
        _factories.TryGetValue(communication.ProtocolKey, out var factory)
            ? factory.Capabilities : CommunicationCapabilities.None;

    public IReadOnlyList<TrustedOpcUaCertificate> GetTrustedOpcUaCertificates() =>
        new OpcUaTrustStore(_opcUaCertificateStoreRoot).List();

    public TrustedOpcUaCertificate TrustOpcUaServerCertificate(string certificatePath) =>
        new OpcUaTrustStore(_opcUaCertificateStoreRoot).TrustFile(certificatePath);

    public bool RemoveTrustedOpcUaServerCertificate(string thumbprint) =>
        new OpcUaTrustStore(_opcUaCertificateStoreRoot).Remove(thumbprint);

    public async Task ConnectAsync(Communication communication, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communication);
        if (communication.IsModbus)
            ModbusDiagnostic(LogLevel.Info, $"运行时连接开始：设备={communication.Name}({communication.Id}), 模式={communication.ModbusMode}, 目标={communication.Host}:{communication.Port}, 串口={communication.SerialPort}, 波特率={communication.BaudRate}, 站号={communication.UnitId}, 请求超时={communication.RequestTimeout}ms");
        if (!_factories.TryGetValue(communication.ProtocolKey, out var factory))
            throw new NotSupportedException($"协议驱动不可用：{communication.ProtocolDisplayName} ({communication.ProtocolKey})。");
        factory.Validate(communication);
        ICommunicationAdapter session = factory.Create(communication);
        if (!_sessions.TryAdd(communication.Id, session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("该通讯设备已经处于连接状态。");
        }

        session.DataReceived += OnDataReceived;
        session.ConnectionClosed += OnConnectionClosed;
        PublishStatus(communication.Device, CommunicationState.Connecting, GetCapabilities(communication));
        try
        {
            await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
            if (!_sessions.TryGetValue(communication.Id, out var current) ||
                !ReferenceEquals(current, session) || !session.IsConnected)
                throw new IOException("通讯连接在建立时已关闭。");
            PublishStatus(communication.Device, CommunicationState.Connected, GetCapabilities(communication));
            if (communication.IsModbus)
                ModbusDiagnostic(LogLevel.Info, $"运行时连接成功：设备={communication.Name}({communication.Id}), 会话类型={session.GetType().Name}, IsConnected={session.IsConnected}");
        }
        catch (Exception ex)
        {
            if (communication.IsModbus)
                ModbusDiagnostic(LogLevel.Error, $"运行时连接失败：设备={communication.Name}({communication.Id}), 错误={ex.Message}", ex);
            _sessions.TryRemove(communication.Id, out _);
            session.DataReceived -= OnDataReceived;
            session.ConnectionClosed -= OnConnectionClosed;
            await session.DisposeAsync().ConfigureAwait(false);
            PublishStatus(communication.Device, CommunicationState.Faulted, GetCapabilities(communication),
                new CommunicationError(ex is OperationCanceledException ? CommunicationErrorCode.Cancelled : CommunicationErrorCode.ConnectionFailed, ex.Message));
            throw;
        }
    }

    public async Task DisconnectAsync(string communicationId)
    {
        if (!_sessions.TryRemove(communicationId, out var session)) return;
        var device = new CommunicationDeviceRef(communicationId);
        var capabilities = GetStatus(device).Capabilities;
        PublishStatus(device, CommunicationState.Disconnecting, capabilities);
        if (session is ModbusSession)
            ModbusDiagnostic(LogLevel.Info, $"运行时断开连接：设备={communicationId}");
        session.DataReceived -= OnDataReceived;
        session.ConnectionClosed -= OnConnectionClosed;
        await session.DisposeAsync().ConfigureAwait(false);
        PublishStatus(device, CommunicationState.Disconnected, capabilities);
    }

    public async Task SendAsync(string communicationId, string text, CancellationToken cancellationToken = default) =>
        _ = await SendBytesAsync(communicationId, Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);

    public async Task<int> SendBytesAsync(string communicationId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(communicationId, out var session))
            throw new InvalidOperationException("设备尚未连接。");
        return await session.SendAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool[]> ReadModbusCoilsAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadCoilsAsync(address, count, cancellationToken);

    public Task<bool[]> ReadModbusInputsAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadInputsAsync(address, count, cancellationToken);

    public Task<ushort[]> ReadModbusHoldingRegistersAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadHoldingRegistersAsync(address, count, cancellationToken);

    public Task<ushort[]> ReadModbusInputRegistersAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadInputRegistersAsync(address, count, cancellationToken);

    public Task WriteModbusCoilAsync(string communicationId, ushort address, bool value, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteCoilAsync(address, value, cancellationToken);

    public Task WriteModbusRegisterAsync(string communicationId, ushort address, ushort value, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteRegisterAsync(address, value, cancellationToken);

    public Task WriteModbusCoilsAsync(string communicationId, ushort address, bool[] values, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteCoilsAsync(address, values, cancellationToken);

    public Task WriteModbusRegistersAsync(string communicationId, ushort address, ushort[] values, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteRegistersAsync(address, values, cancellationToken);

    public Task<byte[]> ReadS7BytesAsync(string communicationId, DataType area, int dataBlock, int startByte, int count,
        CancellationToken cancellationToken = default) => RequireS7Session(communicationId).ReadBytesAsync(area, dataBlock, startByte, count, cancellationToken);

    public Task WriteS7BytesAsync(string communicationId, DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default) => RequireS7Session(communicationId).WriteBytesAsync(area, dataBlock, startByte, value, cancellationToken);

    public Task<IReadOnlyList<OpcUaBrowseItem>> BrowseOpcUaAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).BrowseAsync(nodeId, cancellationToken);

    public Task<DataValue> ReadOpcUaValueAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).ReadValueAsync(nodeId, cancellationToken);

    public Task WriteOpcUaValueAsync(string communicationId, string nodeId, object value, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).WriteValueAsync(nodeId, value, cancellationToken);

    public Task SubscribeOpcUaValueAsync(string communicationId, string nodeId, int samplingIntervalMs, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).SubscribeAsync(nodeId, samplingIntervalMs, cancellationToken);

    public Task UnsubscribeOpcUaValueAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).UnsubscribeAsync(nodeId, cancellationToken);

    public Task<CipExplicitResponse> SendCipExplicitAsync(string communicationId, byte service, uint classId, uint instanceId, uint attributeId,
        ReadOnlyMemory<byte> requestData, CancellationToken cancellationToken = default) =>
        RequireCipSession(communicationId).SendExplicitAsync(service, classId, instanceId, attributeId, requestData, cancellationToken);

    public bool IsConnected(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session.IsConnected;

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _sessions.Keys)
            await DisconnectAsync(id).ConfigureAwait(false);
    }

    private void OnDataReceived(object? sender, CommunicationDataReceivedEventArgs e) =>
        DataReceived?.Invoke(this, e);

    private void OnConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e)
    {
        if (_sessions.TryRemove(e.CommunicationId, out var session))
        {
            session.DataReceived -= OnDataReceived;
            session.ConnectionClosed -= OnConnectionClosed;
            _ = DisposeClosedSessionAsync(session, e.CommunicationId);
        }
        var device = new CommunicationDeviceRef(e.CommunicationId);
        PublishStatus(device, CommunicationState.Disconnected, GetStatus(device).Capabilities);
        ConnectionClosed?.Invoke(this, e);
    }

    private static async Task DisposeClosedSessionAsync(ICommunicationAdapter session, string communicationId)
    {
        try { await session.DisposeAsync().ConfigureAwait(false); }
        catch (Exception ex)
        {
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"释放已断线通讯会话失败：设备={communicationId}, 错误={ex.Message}",
                "CommunicationRuntime", ex, LogTarget.File);
        }
    }

    private static void ValidateSupportedSettings(Communication communication)
    {
        if (communication.ProtocolKey is not ("tcp-client" or "tcp-server" or "udp" or "serial" or "modbus" or "modbus-tcp" or "modbus-rtu" or "s7" or "opcua" or "cip"))
            throw new NotSupportedException($"{communication.ProtocolDisplayName} 的真实协议驱动尚未接入。");
        if (communication.ProtocolKey == "opcua")
        {
            if (!Uri.TryCreate(communication.EndpointUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "opc.tcp")
                throw new ArgumentException("OPC UA 服务端点必须是有效的 opc.tcp:// URL。", nameof(communication));
            if (communication.OpcUaIdentityMode != "匿名")
                throw new NotSupportedException("当前版本的 OPC UA 客户端仅支持匿名身份；用户名/密码凭据按当前需求暂不接入。请将服务器配置为允许匿名访问。");
        }
        else if (communication.ProtocolKey == "cip")
        {
            if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("EtherNet/IP CIP 目标 IP 不能为空。", nameof(communication));
            ValidatePort(communication.Port, "EtherNet/IP CIP 目标端口");
        }
        else if (communication.ProtocolKey == "tcp-client")
        {
            if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("目标 IP 不能为空。", nameof(communication));
            ValidatePort(communication.Port, "目标端口");
        }
        else if (communication.ProtocolKey == "s7")
        {
            if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("S7 目标 IP 不能为空。", nameof(communication));
            ValidatePort(communication.Port, "S7 目标端口");
            if (communication.Rack is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(communication.Rack), "机架号必须为 0 到 7。");
            if (communication.Slot is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(communication.Slot), "槽号必须为 0 到 31。");
            if (!Enum.TryParse<CpuType>(communication.S7CpuType, true, out var s7CpuType) || !Enum.IsDefined(s7CpuType))
                throw new ArgumentException($"不支持的 S7 CPU 类型：{communication.S7CpuType}。", nameof(communication));
        }
        else
        {
            if (communication.ProtocolKey == "serial")
            {
                ValidateSerialSettings(communication);
                return;
            }
            if (communication.IsModbus && !communication.IsModbusRtu)
            {
                if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("Modbus 目标 IP 不能为空。", nameof(communication));
                ValidatePort(communication.Port, "Modbus 目标端口");
                return;
            }
            if (communication.IsModbusRtu)
            {
                ValidateSerialSettings(communication);
                return;
            }
            _ = ParseBindAddress(communication.LocalHost);
            ValidatePort(communication.LocalPort, "本机端口");
            if (communication.ProtocolKey == "udp")
            {
                if (string.IsNullOrWhiteSpace(communication.RemoteHost)) throw new ArgumentException("UDP 目标 IP 不能为空。", nameof(communication));
                ValidatePort(communication.RemotePort, "UDP 目标端口");
            }
        }
    }

    private static void ValidateSerialSettings(Communication communication)
    {
        if (string.IsNullOrWhiteSpace(communication.SerialPort))
            throw new ArgumentException("串口号不能为空。", nameof(communication));
        if (communication.BaudRate is < 50 or > 4_000_000)
            throw new ArgumentOutOfRangeException(nameof(communication.BaudRate), "波特率超出有效范围。");
        if (communication.DataBits is < 5 or > 8)
            throw new ArgumentOutOfRangeException(nameof(communication.DataBits), "数据位必须为 5 到 8。");
        if (communication.StopBits is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(communication.StopBits), "停止位必须为 1 或 2。");
    }

    private ModbusSession RequireModbusSession(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is ModbusSession modbus
            ? modbus
            : throw new InvalidOperationException("Modbus 设备尚未连接，或当前设备不是 Modbus 协议。");

    private S7Session RequireS7Session(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is S7Session s7
            ? s7 : throw new InvalidOperationException("S7 设备尚未连接，或当前设备不是 S7 协议。");

    private OpcUaSession RequireOpcUaSession(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is OpcUaSession opcua
            ? opcua : throw new InvalidOperationException("OPC UA 设备尚未连接，或当前设备不是 OPC UA 协议。");

    private CipSession RequireCipSession(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is CipSession cip
            ? cip : throw new InvalidOperationException("EtherNet/IP CIP 设备尚未连接，或当前设备不是 CIP 协议。");

    private static TimeSpan GetTimeout(Communication communication) =>
        TimeSpan.FromMilliseconds(Math.Clamp(communication.RequestTimeout, 250, 120_000));

    private static IPAddress ParseBindAddress(string value) =>
        string.IsNullOrWhiteSpace(value) ? IPAddress.Any : IPAddress.TryParse(value, out var address)
            ? address : throw new ArgumentException($"本机 IP 地址格式无效：{value}");

    private static void ValidatePort(int port, string name)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(name, "端口必须在 1 到 65535 之间。");
    }

}

public sealed class CommunicationDataReceivedEventArgs : EventArgs
{
    public string CommunicationId { get; }
    public byte[] Data { get; }
    public DateTimeOffset Timestamp { get; }

    public CommunicationDataReceivedEventArgs(string communicationId, byte[] data, DateTimeOffset timestamp)
    {
        CommunicationId = communicationId;
        Data = data;
        Timestamp = timestamp;
    }
}

public sealed class CommunicationConnectionClosedEventArgs : EventArgs
{
    public string CommunicationId { get; }
    public CommunicationConnectionClosedEventArgs(string communicationId) => CommunicationId = communicationId;
}
