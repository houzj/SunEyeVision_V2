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
    private readonly object _stateGate = new();
    private readonly ConcurrentDictionary<string, Task> _retirements = new();
    private readonly ConcurrentDictionary<ICommunicationAdapter, Guid> _connectionOwners = new();
    private readonly IReadOnlyDictionary<string, ICommunicationProtocolFactory> _factories;
    private readonly ConcurrentDictionary<string, CommunicationStatusSnapshot> _statuses = new();
    private readonly ConcurrentDictionary<(string DeviceId, string NodeId), OpcUaPointValueEventArgs> _opcUaValues = new();
    private readonly string _opcUaCertificateStoreRoot;

    private static void ModbusDiagnostic(LogLevel level, string message, Exception? exception = null,
        LogTarget target = LogTarget.File | LogTarget.UI) =>
        VisionLogger.Instance.Log(level, $"[Modbus诊断] {message}", "CommunicationRuntime", exception,
            target);

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
    public event EventHandler<OpcUaPointValueEventArgs>? OpcUaPointValueChanged;

    /// <summary>Checks event identity without probing a transport or taking its state gate.</summary>
    public bool IsCurrentStatus(CommunicationStatusSnapshot status) =>
        _statuses.TryGetValue(status.Device.Value, out var current) && ReferenceEquals(current, status);

    public CommunicationStatusSnapshot GetStatus(CommunicationDeviceRef device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ICommunicationAdapter? connectedSession = null;
        lock (_stateGate)
        {
            if (_statuses.TryGetValue(device.Value, out var published) && published.State == CommunicationState.Connected &&
                _sessions.TryGetValue(device.Value, out var adapter))
                connectedSession = adapter;
        }
        if (connectedSession is OpcUaSession opcUa) opcUa.CheckConnection();
        else if (connectedSession != null && !connectedSession.IsConnected)
            OnConnectionClosed(connectedSession, new CommunicationConnectionClosedEventArgs(device.Value));
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

    public Task ConnectAsync(Communication communication, CancellationToken cancellationToken = default) =>
        ConnectCoreAsync(communication, cancellationToken, null);

    public Task ConnectOwnedAsync(Communication communication, Guid owner, CancellationToken cancellationToken = default) =>
        ConnectCoreAsync(communication, cancellationToken, owner);

    private async Task ConnectCoreAsync(Communication communication, CancellationToken cancellationToken, Guid? owner)
    {
        ArgumentNullException.ThrowIfNull(communication);
        if (communication.IsModbus)
            ModbusDiagnostic(LogLevel.Info, $"运行时连接开始：设备={communication.Name}({communication.Id}), 模式={communication.ModbusMode}, 目标={communication.Host}:{communication.Port}, 串口={communication.SerialPort}, 波特率={communication.BaudRate}, 站号={communication.UnitId}, 请求超时={communication.RequestTimeout}ms");
        if (!_factories.TryGetValue(communication.ProtocolKey, out var factory))
            throw new NotSupportedException($"协议驱动不可用：{communication.ProtocolDisplayName} ({communication.ProtocolKey})。");
        factory.Validate(communication);
        ICommunicationAdapter session;
        bool added;
        while (true)
        {
            Task? retirement;
            lock (_stateGate)
            {
                if (!_retirements.TryGetValue(communication.Id, out retirement) || retirement.IsCompleted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_sessions.ContainsKey(communication.Id))
                        throw new InvalidOperationException("该通讯设备已经处于连接或建连状态。");
                    session = factory.Create(communication);
                    added = _sessions.TryAdd(communication.Id, session);
                    if (added)
                    {
                        if (owner is { } ownerId) _connectionOwners[session] = ownerId;
                        session.DataReceived += OnDataReceived;
                        session.ConnectionClosed += OnConnectionClosed;
                        if (session is OpcUaSession opcUaSession) opcUaSession.PointValueChanged += OnOpcUaPointValueChanged;
                        PublishStatus(communication.Device, CommunicationState.Connecting, GetCapabilities(communication));
                    }
                    break;
                }
            }
            await retirement.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        if (!added)
        {
            await session.DisposeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("该通讯设备已经处于连接状态。");
        }

        // Some transports complete synchronously or cannot abort native creation.
        // Cancellation must invalidate this exact adapter, not a later connection.
        using var cancellationRegistration = cancellationToken.Register(() =>
            OnConnectionClosed(session, new CommunicationConnectionClosedEventArgs(communication.Id, "连接请求已取消。")));
        try
        {
            await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
            lock (_stateGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_sessions.TryGetValue(communication.Id, out var current) ||
                    !ReferenceEquals(current, session) || !session.IsConnected)
                    throw new IOException("通讯连接在建立时已关闭。");
                PublishStatus(communication.Device, CommunicationState.Connected, GetCapabilities(communication));
            }
            if (communication.IsModbus)
                ModbusDiagnostic(LogLevel.Info, $"运行时连接成功：设备={communication.Name}({communication.Id}), 会话类型={session.GetType().Name}, IsConnected={session.IsConnected}");
        }
        catch (Exception ex)
        {
            if (communication.IsModbus)
                ModbusDiagnostic(LogLevel.Error, $"运行时连接失败：设备={communication.Name}({communication.Id}), 错误={ex.Message}", ex);
            Task retirement;
            lock (_stateGate)
            {
                // An old failed attempt must never remove or overwrite a newer connection.
                if (_sessions.TryRemove(new KeyValuePair<string, ICommunicationAdapter>(communication.Id, session)))
                {
                    _connectionOwners.TryRemove(session, out _);
                    PublishStatus(communication.Device, CommunicationState.Faulted, GetCapabilities(communication),
                        new CommunicationError(ex is OperationCanceledException ? CommunicationErrorCode.Cancelled : CommunicationErrorCode.ConnectionFailed, ex.Message));
                    retirement = _retirements[communication.Id] = Task.Run(() => DisposeClosedSessionAsync(session, communication.Id));
                }
                else retirement = Task.Run(() => DisposeClosedSessionAsync(session, communication.Id));
            }
            session.DataReceived -= OnDataReceived;
            session.ConnectionClosed -= OnConnectionClosed;
            if (session is OpcUaSession failedOpcUa) failedOpcUa.PointValueChanged -= OnOpcUaPointValueChanged;
            await retirement.ConfigureAwait(false);
            throw;
        }
    }

    public Task DisconnectAsync(string communicationId) => DisconnectCoreAsync(communicationId, null);

    public Task DisconnectOwnedAsync(string communicationId, Guid owner)
    {
        ICommunicationAdapter expected;
        lock (_stateGate)
        {
            if (!_sessions.TryGetValue(communicationId, out expected!) ||
                !_connectionOwners.TryGetValue(expected, out var currentOwner) || currentOwner != owner)
                return Task.CompletedTask;
        }
        return DisconnectCoreAsync(communicationId, expected);
    }

    private async Task DisconnectCoreAsync(string communicationId, ICommunicationAdapter? expected)
    {
        var device = new CommunicationDeviceRef(communicationId);
        ICommunicationAdapter session;
        CommunicationStatusSnapshot? disconnecting;
        Task retirement;
        lock (_stateGate)
        {
            if (expected != null && (!_sessions.TryGetValue(communicationId, out var active) || !ReferenceEquals(active, expected)))
                return;
            if (!_sessions.TryRemove(communicationId, out session!))
            {
                retirement = _retirements.GetValueOrDefault(communicationId, Task.CompletedTask);
                disconnecting = null;
            }
            else
            {
                _connectionOwners.TryRemove(session, out _);
                ClearOpcUaValues(communicationId);
                var capabilities = _statuses.TryGetValue(communicationId, out var previous)
                    ? previous.Capabilities : CommunicationCapabilities.None;
                PublishStatus(device, CommunicationState.Disconnecting, capabilities);
                disconnecting = _statuses[communicationId];
                session.DataReceived -= OnDataReceived;
                session.ConnectionClosed -= OnConnectionClosed;
                if (session is OpcUaSession opcUaSession) opcUaSession.PointValueChanged -= OnOpcUaPointValueChanged;
                retirement = _retirements[communicationId] = Task.Run(() => DisposeClosedSessionAsync(session, communicationId));
            }
        }
        if (session is ModbusSession)
            ModbusDiagnostic(LogLevel.Info, $"运行时断开连接：设备={communicationId}");
        await retirement.ConfigureAwait(false);
        if (disconnecting == null)
        {
            lock (_stateGate)
                if (!_sessions.ContainsKey(communicationId) && _statuses.TryGetValue(communicationId, out var stopped) &&
                    stopped.State == CommunicationState.Faulted)
                    PublishStatus(device, CommunicationState.Disconnected, stopped.Capabilities);
            return;
        }
        lock (_stateGate)
            if (!_sessions.ContainsKey(communicationId) && _statuses.TryGetValue(communicationId, out var current) &&
                ReferenceEquals(current, disconnecting))
                PublishStatus(device, CommunicationState.Disconnected, disconnecting.Capabilities);
    }

    public async Task SendAsync(string communicationId, string text, CancellationToken cancellationToken = default) =>
        _ = await SendBytesAsync(communicationId, Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);

    public async Task<int> SendBytesAsync(string communicationId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
        => await SendBytesAsync(communicationId, payload, null, cancellationToken).ConfigureAwait(false);

    public async Task<int> SendBytesAsync(string communicationId, ReadOnlyMemory<byte> payload,
        CommunicationStatusSnapshot? expected, CancellationToken cancellationToken)
    {
        return await ExecuteConnectedAsync<ICommunicationAdapter, int>(communicationId,
            session => session.SendAsync(payload, cancellationToken), cancellationToken, expected).ConfigureAwait(false);
    }

    public Task<bool[]> ReadModbusCoilsAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession, bool[]>(communicationId, session => session.ReadCoilsAsync(address, count, cancellationToken), cancellationToken);

    public Task<bool[]> ReadModbusInputsAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession, bool[]>(communicationId, session => session.ReadInputsAsync(address, count, cancellationToken), cancellationToken);

    public Task<ushort[]> ReadModbusHoldingRegistersAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession, ushort[]>(communicationId, session => session.ReadHoldingRegistersAsync(address, count, cancellationToken), cancellationToken);

    public Task<ushort[]> ReadModbusInputRegistersAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession, ushort[]>(communicationId, session => session.ReadInputRegistersAsync(address, count, cancellationToken), cancellationToken);

    public Task WriteModbusCoilAsync(string communicationId, ushort address, bool value, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession>(communicationId, session => session.WriteCoilAsync(address, value, cancellationToken), cancellationToken);

    public Task WriteModbusRegisterAsync(string communicationId, ushort address, ushort value, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession>(communicationId, session => session.WriteRegisterAsync(address, value, cancellationToken), cancellationToken);

    public Task WriteModbusCoilsAsync(string communicationId, ushort address, bool[] values, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession>(communicationId, session => session.WriteCoilsAsync(address, values, cancellationToken), cancellationToken);

    public Task WriteModbusRegistersAsync(string communicationId, ushort address, ushort[] values, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<ModbusSession>(communicationId, session => session.WriteRegistersAsync(address, values, cancellationToken), cancellationToken);

    public Task WriteModbusRegistersAsync(string communicationId, ushort address, ushort[] values,
        CommunicationStatusSnapshot? expected, CancellationToken cancellationToken) =>
        ExecuteConnectedAsync<ModbusSession>(communicationId, session => session.WriteRegistersAsync(address, values, cancellationToken), cancellationToken, expected);

    public Task<byte[]> ReadS7BytesAsync(string communicationId, DataType area, int dataBlock, int startByte, int count,
        CancellationToken cancellationToken = default) => ExecuteConnectedAsync<S7Session, byte[]>(communicationId,
            session => session.ReadBytesAsync(area, dataBlock, startByte, count, cancellationToken), cancellationToken);

    public Task WriteS7BytesAsync(string communicationId, DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default) => ExecuteConnectedAsync<S7Session>(communicationId,
            session => session.WriteBytesAsync(area, dataBlock, startByte, value, cancellationToken), cancellationToken);

    public Task<IReadOnlyList<OpcUaBrowseItem>> BrowseOpcUaAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<OpcUaSession, IReadOnlyList<OpcUaBrowseItem>>(communicationId,
            session => session.BrowseAsync(nodeId, cancellationToken), cancellationToken);

    public async Task<DataValue> ReadOpcUaValueAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default)
    {
        var session = RequireOpcUaSession(communicationId);
        var value = await session.ReadValueAsync(nodeId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCurrentOpcUaSession(communicationId, session);
        return value;
    }

    public async Task<OpcUaPointValueEventArgs> RefreshOpcUaPointAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default)
    {
        var session = RequireOpcUaSession(communicationId);
        var value = await session.ReadPointAsync(nodeId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureCurrentOpcUaSession(communicationId, session);
        OnOpcUaPointValueChanged(session, value);
        return value;
    }

    public bool TryGetLatestOpcUaPoint(string communicationId, string nodeId, out OpcUaPointValueEventArgs? value) =>
        _opcUaValues.TryGetValue((communicationId, nodeId), out value);

    public void ReportOpcUaPointError(string communicationId, string nodeId, string message)
    {
        OnOpcUaPointValueChanged(this, new OpcUaPointValueEventArgs(communicationId, nodeId,
            null, BuiltInType.Null, false, StatusCodes.Bad, DateTimeOffset.UtcNow, message));
    }

    public Task WriteOpcUaValueAsync(string communicationId, string nodeId, object value, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<OpcUaSession>(communicationId,
            session => session.WriteValueAsync(nodeId, value, cancellationToken), cancellationToken);

    public Task WriteOpcUaValueAsync(string communicationId, string nodeId, object value,
        CommunicationStatusSnapshot? expected, CancellationToken cancellationToken) =>
        ExecuteConnectedAsync<OpcUaSession>(communicationId,
            session => session.WriteValueAsync(nodeId, value, cancellationToken), cancellationToken, expected);

    public Task SubscribeOpcUaValueAsync(string communicationId, string nodeId, int samplingIntervalMs, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<OpcUaSession>(communicationId,
            session => session.SubscribeAsync(nodeId, samplingIntervalMs, cancellationToken), cancellationToken);

    public Task UnsubscribeOpcUaValueAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<OpcUaSession>(communicationId,
            session => session.UnsubscribeAsync(nodeId, cancellationToken), cancellationToken);
    public Task<(double Sampling, double Publishing)?> GetOpcUaMonitoringIntervalsAsync(string communicationId,
        string nodeId, CancellationToken token = default) => ExecuteConnectedAsync<OpcUaSession, (double Sampling, double Publishing)?>(communicationId,
            session => session.GetMonitoringIntervalsAsync(nodeId, token), token);

    public Task<CipExplicitResponse> SendCipExplicitAsync(string communicationId, byte service, uint classId, uint instanceId, uint attributeId,
        ReadOnlyMemory<byte> requestData, CancellationToken cancellationToken = default) =>
        ExecuteConnectedAsync<CipSession, CipExplicitResponse>(communicationId,
            session => session.SendExplicitAsync(service, classId, instanceId, attributeId, requestData, cancellationToken), cancellationToken);

    public bool IsConnected(string communicationId)
    {
        _ = GetStatus(new CommunicationDeviceRef(communicationId));
        return _sessions.TryGetValue(communicationId, out var session) && session.IsConnected;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _sessions.Keys)
            await DisconnectAsync(id).ConfigureAwait(false);
        await Task.WhenAll(_retirements.Values).ConfigureAwait(false);
        foreach (var retirement in _retirements.ToArray())
            if (retirement.Value.IsCompleted) _retirements.TryRemove(retirement);
        lock (_stateGate)
        {
            // This runtime is reused after a solution switch. Old event identities
            // and capability/error snapshots must not survive into the next epoch.
            if (_sessions.IsEmpty)
            {
                _statuses.Clear();
                _opcUaValues.Clear();
            }
        }
    }

    private void OnDataReceived(object? sender, CommunicationDataReceivedEventArgs e)
    {
        if (sender is ICommunicationAdapter adapter &&
            (!_sessions.TryGetValue(e.CommunicationId, out var active) || !ReferenceEquals(active, adapter) || !adapter.IsConnected)) return;
        DataReceived?.Invoke(this, e);
    }

    private void OnOpcUaPointValueChanged(object? sender, OpcUaPointValueEventArgs value)
    {
        if (!_sessions.TryGetValue(value.CommunicationId, out var active) || active is not OpcUaSession || !active.IsConnected ||
            sender is OpcUaSession && !ReferenceEquals(sender, active))
            return;
        _opcUaValues[(value.CommunicationId, value.NodeId)] = value;
        OpcUaPointValueChanged?.Invoke(this, value);
    }

    private void OnConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e)
    {
        CommunicationStatusSnapshot closedStatus;
        lock (_stateGate)
        {
            if (!_sessions.TryGetValue(e.CommunicationId, out var active) || !ReferenceEquals(active, sender) ||
                !_sessions.TryRemove(new KeyValuePair<string, ICommunicationAdapter>(e.CommunicationId, active)))
                return;
            _connectionOwners.TryRemove(active, out _);
            ClearOpcUaValues(e.CommunicationId);
            active.DataReceived -= OnDataReceived;
            active.ConnectionClosed -= OnConnectionClosed;
            if (active is OpcUaSession closedOpcUa) closedOpcUa.PointValueChanged -= OnOpcUaPointValueChanged;
            // Never close the native Session on its own KeepAlive/notification callback.
            _retirements[e.CommunicationId] = Task.Run(() => DisposeClosedSessionAsync(active, e.CommunicationId));
            var device = new CommunicationDeviceRef(e.CommunicationId);
            PublishStatus(device, CommunicationState.Disconnected, GetStatus(device).Capabilities,
                new CommunicationError(CommunicationErrorCode.ConnectionFailed, e.Reason ?? "连接已中断，底层会话不可用。"));
            closedStatus = _statuses[e.CommunicationId];
        }
        // Reconnect policy belongs to the application service, not to the state lock.
        ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(e.CommunicationId, e.Reason, closedStatus));
    }

    private void ClearOpcUaValues(string communicationId)
    {
        foreach (var key in _opcUaValues.Keys.Where(key => key.DeviceId == communicationId))
            _opcUaValues.TryRemove(key, out _);
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

    private OpcUaSession RequireOpcUaSession(string communicationId)
    {
        lock (_stateGate)
        {
            // A registered adapter is not ready until ConnectAsync has completed.
            // Never let a read during Connecting invalidate the Session being created.
            if (_statuses.TryGetValue(communicationId, out var status) && status.State == CommunicationState.Connected &&
                _sessions.TryGetValue(communicationId, out var session) && session is OpcUaSession opcua)
                return opcua;
            throw new InvalidOperationException("OPC UA 设备尚未连接或正在重连，请等待连接恢复后重新执行。");
        }
    }

    private void EnsureCurrentOpcUaSession(string communicationId, OpcUaSession expected)
    {
        expected.CheckConnection();
        lock (_stateGate)
            if (!_sessions.TryGetValue(communicationId, out var current) || !ReferenceEquals(current, expected) || !expected.IsConnected)
                throw new InvalidOperationException("OPC UA 读取期间连接已失效或更换，本次读取结果未采用，请重新执行。");
    }

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
    public string? Reason { get; }
    public CommunicationStatusSnapshot? RuntimeStatus { get; }
    public CommunicationConnectionClosedEventArgs(string communicationId) : this(communicationId, null) { }
    public CommunicationConnectionClosedEventArgs(string communicationId, string? reason)
    {
        CommunicationId = communicationId;
        Reason = reason;
    }
    internal CommunicationConnectionClosedEventArgs(string communicationId, string? reason, CommunicationStatusSnapshot status)
        : this(communicationId, reason) => RuntimeStatus = status;
}
